using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ninject;
using Ninject.Modules;
using Ninject.Parameters;
using BitSharp.Node.Storage;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;

namespace BitSharp.SqlServer.Azure
{
    public class SqlStorageModule : NinjectModule
    {
        private readonly RulesEnum rulesType;

        public SqlStorageModule(RulesEnum rulesType)
        {
            this.rulesType = rulesType;
        }

        public override void Load()
        {
            // bind concrete storage providers
            this.Bind<SqlStorageModule>().ToSelf().InSingletonScope().WithConstructorArgument("rulesType", this.rulesType);
            
            this.Bind<NetworkPeerStorage>().ToSelf().InSingletonScope()
            //    .WithConstructorArgument("baseDirectory", this.peersDirectory)
                .WithConstructorArgument("rulesType", this.rulesType);

            // bind storage providers interfaces
            this.Bind<IStorageManager>().ToMethod(x => this.Kernel.Get<SqlStorageManager>()).InSingletonScope();
            
            //HACK:  USE ESENT FOR NOW
            //this.Bind<INetworkPeerStorage>().ToMethod(x => this.Kernel.Get<NetworkPeerStorage>()).InSingletonScope();
            this.Bind<INetworkPeerStorage>().ToMethod(x => this.Kernel.Get<NetworkPeerStorage>()).InSingletonScope();
        }
    }
}
