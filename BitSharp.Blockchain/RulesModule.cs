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
        TestNet2,
        TestNet3,
        ComparisonToolTestNet
    }

    public class RulesModule : NinjectModule
    {
        private readonly RulesEnum rules;

        public RulesModule(RulesEnum rules)
        {
            this.rules = rules;
        }

        public override void Load()
        {
            this.Bind<RulesEnum>().ToConstant(this.rules);

            switch (this.rules)
            {
                case RulesEnum.MainNet:
                    this.Bind<IBlockchainRules>().To<MainnetRules>().InSingletonScope();
                    break;

                case RulesEnum.TestNet2:
                case RulesEnum.ComparisonToolTestNet:
                    this.Bind<IBlockchainRules>().To<Testnet2Rules>().InSingletonScope();
                    break;
                
                case RulesEnum.TestNet3:
                    this.Bind<IBlockchainRules>().To<Testnet3Rules>().InSingletonScope();
                    break;
            }
        }
    }
}
