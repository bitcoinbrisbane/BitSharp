using BitSharp.Common;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Storage
{
    public interface IUtxoStorage : IDisposable
    {
        UInt256 BlockHash { get; }

        
        int TransactionCount { get; }

        bool ContainsTransaction(UInt256 txHash);

        bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx);

        IEnumerable<KeyValuePair<UInt256, UnspentTx>> UnspentTransactions();

        
        int OutputCount { get; }

        bool ContainsOutput(TxOutputKey txOutputKey);

        bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput);

        IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> UnspentOutputs();
        
        
        void DisposeDelete();
    }
}
