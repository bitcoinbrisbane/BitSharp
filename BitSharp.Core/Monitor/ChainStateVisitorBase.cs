using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Monitor
{
    public abstract class ChainStateVisitorBase : IChainStateVisitor
    {
        public virtual void BeginBlock(ChainedHeader chainedHeader) { }

        
        public virtual void BeforeAddTransaction(ChainPosition chainPosition, Transaction tx) { }

        public virtual void CoinbaseInput(ChainPosition chainPosition, TxInput txInput) { }

        public virtual void MintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase) { }

        public virtual void SpendTxOutput(ChainPosition chainPosition, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash) { }

        public virtual void AfterAddTransaction(ChainPosition chainPosition, Transaction tx) { }

        
        public virtual void BeforeRemoveTransaction(ChainPosition chainPosition, Transaction tx) { }

        public virtual void UnCoinbaseInput(ChainPosition chainPosition, TxInput txInput) { }

        public virtual void UnmintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase) { }

        public virtual void UnspendTxOutput(ChainPosition chainPosition, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash) { }

        public virtual void AfterRemoveTransaction(ChainPosition chainPosition, Transaction tx) { }

        
        public virtual void CommitBlock(ChainedHeader chainedHeader) { }

        public virtual void RollbackBlock(ChainedHeader chainedHeader) { }
    }
}
