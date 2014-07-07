using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public interface IChainStateBuilderStorage : IDisposable
    {
        void BeginTransaction();
        void CommitTransaction();
        void RollbackTransaction();


        int BlockHeight { get; set; }

        UInt256 BlockHash { get; set; }

        
        int TransactionCount { get; }

        bool ContainsTransaction(UInt256 txHash);

        bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx);

        bool TryGetTransaction(UInt256 txHash, int spentBlockIndex, out UnspentTx unspentTx);

        bool TryAddTransaction(UInt256 txHash, UnspentTx unspentTx);

        bool RemoveTransaction(UInt256 txHash, int spentBlockIndex);

        void UpdateTransaction(UInt256 txHash, UnspentTx unspentTx);

        IEnumerable<KeyValuePair<UInt256, UnspentTx>> UnspentTransactions();


        //int OutputCount { get; }

        //bool ContainsOutput(TxOutputKey txOutputKey);

        //bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput);

        //void AddOutput(TxOutputKey txOutputKey, TxOutput txOutput);

        //bool RemoveOutput(TxOutputKey txOutputKey);

        //IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> UnspentOutputs();


        void Flush();

        IChainStateStorage ToImmutable();
    }
}
