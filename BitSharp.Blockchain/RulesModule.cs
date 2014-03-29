using Ninject.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public enum RulesEnum
    {
        MainNet,
        TestNet3,
        ComparisonToolTestNet
    }

    public class RulesModule : NinjectModule
    {
        private readonly RulesEnum networkRules;

        public RulesModule(RulesEnum networkRules)
        {
            this.networkRules = networkRules;
        }

        public override void Load()
        {
            switch (this.networkRules)
            {
                case RulesEnum.MainNet:
                    this.Bind<IBlockchainRules>().To<MainnetRules>().InSingletonScope();
                    break;

                case RulesEnum.TestNet3:
                case RulesEnum.ComparisonToolTestNet:
                    this.Bind<IBlockchainRules>().To<MainnetRules>().InSingletonScope();
                    break;
            }
        }
    }
}
