using BitSharp.Common;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class GenesisUtxoStorage : IUtxoStorage
    {
        // genesis block coinbase is not included in utxo, it is unspendable

        private readonly UInt256 blockHash;

        public GenesisUtxoStorage(UInt256 blockHash)
        {
            this.blockHash = blockHash;
        }

        public UInt256 BlockHash
        {
            get { return this.blockHash; }
        }

        public int TransactionCount
        {
            get { return 0; }
        }

        public bool ContainsTransaction(UInt256 txHash)
        {
            return false;
        }

        public bool TryGetTransaction(UInt256 txHash, out OutputStates outputStates)
        {
            outputStates = default(OutputStates);
            return false;
        }

        public IEnumerable<KeyValuePair<UInt256, OutputStates>> UnspentTransactions()
        {
            return Enumerable.Empty<KeyValuePair<UInt256, OutputStates>>();
        }

        public int OutputCount
        {
            get { return 0; }
        }

        public bool ContainsOutput(TxOutputKey txOutputKey)
        {
            return false;
        }

        public bool TryGetOutput(TxOutputKey txOutputKey, out TxOutput txOutput)
        {
            txOutput = default(TxOutput);
            return false;
        }

        public IEnumerable<KeyValuePair<TxOutputKey, TxOutput>> UnspentOutputs()
        {
            return Enumerable.Empty<KeyValuePair<TxOutputKey, TxOutput>>();
        }

        public void DisposeDelete()
        {
        }

        public void Dispose()
        {
        }
    }
}
