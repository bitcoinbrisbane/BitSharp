using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using BitSharp.Core.Storage.Memory;
using BitSharp.Node.Storage;
using Microsoft.Isam.Esent.Collections.Generic;
using Microsoft.Isam.Esent.Interop;
using Ninject;
using Ninject.Modules;
using Ninject.Parameters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Esent
{
    public class EsentStorageModule : NinjectModule
    {
        private readonly string baseDirectory;
        private readonly RulesEnum rulesType;
        private readonly long? cacheSizeMinBytes;
        private readonly long? cacheSizeMaxBytes;

        public EsentStorageModule(string baseDirectory, RulesEnum rulesType, long? cacheSizeMinBytes = null, long? cacheSizeMaxBytes = null)
        {
            this.baseDirectory = Path.Combine(baseDirectory, rulesType.ToString());
            this.rulesType = rulesType;
            this.cacheSizeMinBytes = cacheSizeMinBytes;
            this.cacheSizeMaxBytes = cacheSizeMaxBytes;
        }

        public override void Load()
        {
            //TODO remove reflection once PersistentDictionary is phased out
            var esentAssembly = typeof(PersistentDictionary<string, string>).Assembly;
            var type = esentAssembly.GetType("Microsoft.Isam.Esent.Collections.Generic.CollectionsSystemParameters");
            var method = type.GetMethod("Init");
            method.Invoke(null, null);
            if (this.cacheSizeMinBytes != null)
                SystemParameters.CacheSizeMin = (this.cacheSizeMinBytes.Value / SystemParameters.DatabasePageSize).ToIntChecked();
            if (this.cacheSizeMaxBytes != null)
                SystemParameters.CacheSizeMax = (this.cacheSizeMaxBytes.Value / SystemParameters.DatabasePageSize).ToIntChecked();

            // bind concrete storage providers
            this.Bind<EsentStorageManager>().ToSelf().InSingletonScope().WithConstructorArgument("baseDirectory", this.baseDirectory);
            this.Bind<NetworkPeerStorage>().ToSelf().InSingletonScope()
                .WithConstructorArgument("baseDirectory", this.baseDirectory)
                .WithConstructorArgument("rulesType", this.rulesType);

            // bind storage providers interfaces
            this.Bind<IStorageManager>().ToMethod(x => this.Kernel.Get<EsentStorageManager>()).InSingletonScope();
            this.Bind<INetworkPeerStorage>().ToMethod(x => this.Kernel.Get<NetworkPeerStorage>()).InSingletonScope();
        }
    }
}
