using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using Microsoft.Isam.Esent.Collections.Generic;
using Microsoft.Isam.Esent.Interop;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Esent
{
    public class ChainStateStorage : IChainStateStorage
    {
        private ChainStateBuilderStorage storage;

        internal ChainStateStorage(ChainStateBuilderStorage parentStorage)
        {
            this.storage = new ChainStateBuilderStorage(parentStorage);
        }

        public UInt256 BlockHash
        {
            get { return this.storage.BlockHash; }
        }

        public int TransactionCount
        {
            get { return this.storage.TransactionCount; }
        }

        public bool ContainsTransaction(UInt256 txHash)
        {
            return this.storage.ContainsTransaction(txHash);
        }

        public bool TryGetTransaction(UInt256 txHash, out UnspentTx unspentTx)
        {
            return this.storage.TryGetTransaction(txHash, out unspentTx);
        }

        public IEnumerable<KeyValuePair<UInt256, UnspentTx>> UnspentTransactions()
        {
            return this.storage.UnspentTransactions();
        }

        public int OutputCount
        {
            get { return this.storage.OutputCount; }
        }

        public bool ContainsOutput(TxOutputKey txOutputKey)
        {
            return this.storage.ContainsOutput(txOutputKey);
        }

        public bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput)
        {
            return this.storage.TryGetOutput(txOutputKey, out txOutput);
        }

        public IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> UnspentOutputs()
        {
            return this.storage.UnspentOutputs();
        }

        public void Dispose()
        {
            this.storage.Dispose();
        }
    }
}
