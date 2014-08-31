using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core
{
    public interface ICoreDaemon
    {
        /// <summary>
        /// Retrieve the chain for the current processed chain state.
        /// </summary>
        Chain CurrentChain { get; }
    }
}
