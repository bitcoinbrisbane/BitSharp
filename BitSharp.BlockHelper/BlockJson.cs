using BitSharp.Common;
using BitSharp.Core.Domain;
using BitSharp.Core.Script;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.BlockHelper
{
    public static class BlockJson
    {
        public static Block GetBlockFromJson(string blockJson)
        {
            var block = JsonConvert.DeserializeObject<JsonBlock>(blockJson);
            return new Block
            (
                header: new BlockHeader
                (
                    version: Convert.ToUInt32(block.ver),
                    previousBlock: UInt256.Parse(block.prev_block, NumberStyles.HexNumber),
                    merkleRoot: UInt256.Parse(block.mrkl_root, NumberStyles.HexNumber),
                    time: Convert.ToUInt32(block.time),
                    bits: Convert.ToUInt32(block.bits),
                    nonce: Convert.ToUInt32(block.nonce)
                ),
                transactions: ReadTransactions(block.tx)
            );
        }

        public static ImmutableArray<Transaction> ReadTransactions(JsonTransaction[] transactions)
        {
            return
                Enumerable.Range(0, (int)transactions.Length)
                .Select(i => (Transaction)ReadTransaction(transactions[i]))
                .ToImmutableArray();
        }

        public static Transaction ReadTransaction(JsonTransaction transaction)
        {
            return new Transaction
            (
                version: Convert.ToUInt32(transaction.ver),
                inputs: ReadInputs(transaction.@in),
                outputs: ReadOutputs(transaction.@out),
                lockTime: Convert.ToUInt32(transaction.lock_time)
            );
        }

        public static ImmutableArray<TxInput> ReadInputs(JsonTxInput[] inputs)
        {
            return
                Enumerable.Range(0, (int)inputs.Length)
                .Select(i => (TxInput)ReadInput(inputs[i]))
                .ToImmutableArray();
        }

        public static ImmutableArray<TxOutput> ReadOutputs(JsonTxOutput[] outputs)
        {
            return
                Enumerable.Range(0, (int)outputs.Length)
                .Select(i => (TxOutput)ReadOutput(outputs[i]))
                .ToImmutableArray();
        }

        public static TxInput ReadInput(JsonTxInput input)
        {
            return new TxInput
            (
                previousTxOutputKey: new TxOutputKey
                (
                    txHash: UInt256.Parse(input.prev_out.hash, NumberStyles.HexNumber),
                    txOutputIndex: Convert.ToUInt32(input.prev_out.n)
                ),
                scriptSignature: input.scriptSig != null ? ReadScript(input.scriptSig) : ReadCoinbase(input.coinbase),
                sequence: input.sequence != null ? Convert.ToUInt32(input.sequence) : 0xFFFFFFFF
            );
        }

        public static TxOutput ReadOutput(JsonTxOutput output)
        {
            return new TxOutput
            (
                value: Convert.ToUInt64(((string)output.value).Replace(".", "")), //TODO cleaner decimal replace
                scriptPublicKey: ReadScript(output.scriptPubKey)
            );
        }

        public static ImmutableArray<byte> ReadCoinbase(string data)
        {
            return data != null ? HexStringToByteArray(data) : ImmutableArray.Create<byte>();
        }

        public static ImmutableArray<byte> ReadScript(string data)
        {
            if (data == null)
                return ImmutableArray.Create<byte>();

            var bytes = new List<byte>();
            foreach (var x in data.Split(' '))
            {
                if (x.StartsWith("OP_"))
                {
                    bytes.Add((byte)(int)Enum.Parse(typeof(ScriptOp), x));
                }
                else
                {
                    var pushBytes = HexStringToByteArray(x);
                    if (pushBytes.Count >= (int)ScriptOp.OP_PUSHBYTES1 && pushBytes.Count <= (int)ScriptOp.OP_PUSHBYTES75)
                    {
                        bytes.Add((byte)pushBytes.Count);
                        bytes.AddRange(pushBytes);
                    }
                    else
                    {
                        throw new Exception("data is too long");
                    }
                }
            }

            return bytes.ToImmutableArray();
        }

        //TODO not actually an extension method...
        private static ImmutableArray<byte> HexStringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToImmutableArray();
        }

        public sealed class JsonBlock
        {
            public string ver { get; set; }
            public string prev_block { get; set; }
            public string mrkl_root { get; set; }
            public string time { get; set; }
            public string bits { get; set; }
            public string nonce { get; set; }
            public JsonTransaction[] tx { get; set; }
        }

        public sealed class JsonTransaction
        {
            public string ver { get; set; }
            public JsonTxInput[] @in { get; set; }
            public JsonTxOutput[] @out { get; set; }
            public string lock_time { get; set; }
        }

        public sealed class JsonTxInput
        {
            public JsonPrevTxOutput prev_out { get; set; }
            public string scriptSig { get; set; }
            public string coinbase { get; set; }
            public string sequence { get; set; }
        }

        public sealed class JsonPrevTxOutput
        {
            public string hash { get; set; }
            public string n { get; set; }
        }

        public sealed class JsonTxOutput
        {
            public string value { get; set; }
            public string scriptPubKey { get; set; }
        }
    }
}
