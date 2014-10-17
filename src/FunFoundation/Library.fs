module FunFoundation
#nowarn "9"

type uint8_t = byte

type FdbError = int
type FDBFuture = nativeint
type FDBDatabase = nativeint
type FDBTransaction = nativeint
type FutureHandle = nativeint
type FDBCluster = nativeint
type FDBMutationType =
    | Invalid = 0
    | Add = 2
    | BitAnd = 6
    | BitOr = 7
    | BitXor = 8

type FDBCallback = delegate of nativeint * nativeint -> unit
module Native =
    open System.Runtime.InteropServices

    type FDBTransactionPtr = nativeptr<FDBTransaction>
    type FDBDatabasePtr = nativeptr<FDBDatabase>

#if MONO
    [<Literal>]
    let FdbDll = "libfdb_c.so"
#else
    [<Literal>]
    let FdbDll = """C:\Program Files\foundationdb\bin\fdb_c.dll"""
#endif

    [<DllImport(FdbDll, CallingConvention = CallingConvention.Cdecl)>]
    extern FdbError fdb_future_set_callback(FDBFuture f, FDBCallback callback, void* callback_parameter)
    
    [<DllImport(FdbDll, CallingConvention = CallingConvention.Cdecl)>]
    extern FdbError fdb_database_create_transaction(FDBDatabase d, FDBTransactionPtr out_transaction)
    
    [<DllImport(FdbDll, CallingConvention = CallingConvention.Cdecl)>]
    extern FdbError fdb_future_get_database(FDBFuture future, FDBDatabase& out_database)

    [<DllImport(FdbDll, CallingConvention = CallingConvention.Cdecl)>]
    extern void fdb_transaction_atomic_op(FDBTransaction transaction, uint8_t* key_name, int key_name_length, uint8_t* param, int param_length, FDBMutationType operationType)

    [<DllImport(FdbDll, CallingConvention = CallingConvention.Cdecl)>]
    extern void fdb_transaction_commit(FDBTransaction transaction);

    [<DllImport(FdbDll, CallingConvention = CallingConvention.Cdecl)>]
    extern FDBFuture fdb_create_cluster([<MarshalAs(UnmanagedType.LPStr)>]string cluster_file_path)
    
    [<DllImport(FdbDll, CallingConvention = CallingConvention.Cdecl)>]
    extern void fdb_cluster_destroy(FDBCluster cluster)

type Fdb<'a> =
    | Success of 'a
    | TransactionFailed
    | FatalError

type FoundationBuilder() =
    member __.Bind(x, f) =
        async.Bind(x, fun i ->
            match i with
            | Success result -> f result
            | TransactionFailed -> async { return TransactionFailed }
            | FatalError -> async { return FatalError })
    member __.Delay f = f()
    member __.Return x = async { return Success x }
    member __.Zero() =  async { return Success () }

let fdb = FoundationBuilder()

module FoundationDb =
    open System.Threading
    open Microsoft.FSharp.NativeInterop
    open Native

    let getFuture ptr =
        async {
            use waitHandle = new AutoResetEvent(false)
            let result = ref 0n
            let callback = FDBCallback(fun future _ ->
                result := future
                waitHandle.Set() |> ignore)
            let error = fdb_future_set_callback(ptr, callback, 0n)
            match error with
            | 0 -> do! Async.AwaitWaitHandle waitHandle |> Async.Ignore
                   return Success !result
            | _ -> return FatalError
        }

    let database future =
        async {
            let mutable database = 0n
            let error = fdb_future_get_database(future, &database)
            match error with
            | 0 -> return Success database
            | _ -> return FatalError
        }

    let cluster f =
        fdb {
            let clusterFuture = fdb_create_cluster(null)
            let! cluster = getFuture clusterFuture
            let result = f cluster
            fdb_cluster_destroy(cluster)
            return result
        }
        
    let transaction f =
        fdb {
            let clusterFuture = fdb_create_cluster(null)
            let! cluster = getFuture clusterFuture
            let result = f cluster
            fdb_cluster_destroy(cluster)
            return result 
        }
