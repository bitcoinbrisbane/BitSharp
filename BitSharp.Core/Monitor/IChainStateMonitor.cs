using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Monitor
{
    public interface IChainStateMonitor
    {
        void BeginBlock(ChainedHeader chainedHeader);


        void BeforeAddTransaction(ChainPosition chainPosition, Transaction tx);

        void CoinbaseInput(ChainPosition chainPosition, TxInput txInput);

        void MintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase);

        void SpendTxOutput(ChainPosition chainPosition, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash);

        void AfterAddTransaction(ChainPosition chainPosition, Transaction tx);


        void BeforeRemoveTransaction(ChainPosition chainPosition, Transaction tx);

        void UnCoinbaseInput(ChainPosition chainPosition, TxInput txInput);

        void UnmintTxOutput(ChainPosition chainPosition, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash, bool isCoinbase);

        void UnspendTxOutput(ChainPosition chainPosition, TxInput txInput, TxOutputKey txOutputKey, TxOutput txOutput, UInt256 outputScriptHash);

        void AfterRemoveTransaction(ChainPosition chainPosition, Transaction tx);


        void CommitBlock(ChainedHeader chainedHeader);

        void RollbackBlock(ChainedHeader chainedHeader);
    }
}
