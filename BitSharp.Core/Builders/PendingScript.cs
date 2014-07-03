using BitSharp.Core.Domain;
using BitSharp.Core.Monitor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    public class PendingScript
    {
        public readonly ChainedHeader chainedHeader;
        public readonly Transaction transaction;
        public readonly int txIndex;
        public readonly TxInput txInput;
        public readonly int inputIndex;
        public readonly TxOutput prevTxOutput;

        public PendingScript(ChainedHeader chainedHeader, Transaction transaction, int txIndex, TxInput txInput, int inputIndex, TxOutput prevTxOutput)
        {
            this.chainedHeader = chainedHeader;
            this.transaction = transaction;
            this.txIndex = txIndex;
            this.txInput = txInput;
            this.inputIndex = inputIndex;
            this.prevTxOutput = prevTxOutput;
        }
    }
}
