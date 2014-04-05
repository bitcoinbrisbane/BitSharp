using BitSharp.Data;
using BitSharp.Common.ExtensionMethods;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data.Test
{
    [TestClass]
    public partial class DataEncoderTest
    {
        [TestMethod]
        public void TestWireEncodeAddressPayload()
        {
            var actual = DataEncoder.EncodeAddressPayload(ADDRESS_PAYLOAD_1);
            CollectionAssert.AreEqual(ADDRESS_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeAddressPayload()
        {
            var actual = DataEncoder.EncodeAddressPayload(DataEncoder.DecodeAddressPayload(ADDRESS_PAYLOAD_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(ADDRESS_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeAlertPayload()
        {
            var actual = DataEncoder.EncodeAlertPayload(ALERT_PAYLOAD_1);
            CollectionAssert.AreEqual(ALERT_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeAlertPayload()
        {
            var actual = DataEncoder.EncodeAlertPayload(DataEncoder.DecodeAlertPayload(ALERT_PAYLOAD_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(ALERT_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeBlockHeader()
        {
            var actual = DataEncoder.EncodeBlockHeader(BLOCK_HEADER_1);
            CollectionAssert.AreEqual(BLOCK_HEADER_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeBlockHeader()
        {
            var actual = DataEncoder.EncodeBlockHeader(DataEncoder.DecodeBlockHeader(BLOCK_HEADER_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(BLOCK_HEADER_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeBlock()
        {
            var actual = DataEncoder.EncodeBlock(BLOCK_1);
            CollectionAssert.AreEqual(BLOCK_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeBlock()
        {
            var actual = DataEncoder.EncodeBlock(DataEncoder.DecodeBlock(BLOCK_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(BLOCK_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeGetBlocksPayload()
        {
            var actual = DataEncoder.EncodeGetBlocksPayload(GET_BLOCKS_PAYLOAD_1);
            CollectionAssert.AreEqual(GET_BLOCKS_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeGetBlocksPayload()
        {
            var actual = DataEncoder.EncodeGetBlocksPayload(DataEncoder.DecodeGetBlocksPayload(GET_BLOCKS_PAYLOAD_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(GET_BLOCKS_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeInventoryPayload()
        {
            var actual = DataEncoder.EncodeInventoryPayload(INVENTORY_PAYLOAD_1);
            CollectionAssert.AreEqual(INVENTORY_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeInventoryPayload()
        {
            var actual = DataEncoder.EncodeInventoryPayload(DataEncoder.DecodeInventoryPayload(INVENTORY_PAYLOAD_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(INVENTORY_PAYLOAD_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeInventoryVector()
        {
            var actual = DataEncoder.EncodeInventoryVector(INVENTORY_VECTOR_1);
            CollectionAssert.AreEqual(INVENTORY_VECTOR_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeInventoryVector()
        {
            var actual = DataEncoder.EncodeInventoryVector(DataEncoder.DecodeInventoryVector(INVENTORY_VECTOR_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(INVENTORY_VECTOR_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeMessage()
        {
            var actual = DataEncoder.EncodeMessage(MESSAGE_1);
            CollectionAssert.AreEqual(MESSAGE_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeMessage()
        {
            var actual = DataEncoder.EncodeMessage(DataEncoder.DecodeMessage(MESSAGE_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(MESSAGE_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeNetworkAddress()
        {
            var actual = DataEncoder.EncodeNetworkAddress(NETWORK_ADDRESS_1);
            CollectionAssert.AreEqual(NETWORK_ADDRESS_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeNetworkAddress()
        {
            var actual = DataEncoder.EncodeNetworkAddress(DataEncoder.DecodeNetworkAddress(NETWORK_ADDRESS_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(NETWORK_ADDRESS_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeNetworkAddressWithTime()
        {
            var actual = DataEncoder.EncodeNetworkAddressWithTime(NETWORK_ADDRESS_WITH_TIME_1);
            CollectionAssert.AreEqual(NETWORK_ADDRESS_WITH_TIME_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeNetworkAddressWithTime()
        {
            var actual = DataEncoder.EncodeNetworkAddressWithTime(DataEncoder.DecodeNetworkAddressWithTime(NETWORK_ADDRESS_WITH_TIME_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(NETWORK_ADDRESS_WITH_TIME_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeTransactionIn()
        {
            var actual = DataEncoder.EncodeTxInput(TRANSACTION_INPUT_1);
            CollectionAssert.AreEqual(TRANSACTION_INPUT_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeTransactionIn()
        {
            var actual = DataEncoder.EncodeTxInput(DataEncoder.DecodeTxInput(TRANSACTION_INPUT_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(TRANSACTION_INPUT_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeTransactionOut()
        {
            var actual = DataEncoder.EncodeTxOutput(TRANSACTION_OUTPUT_1);
            CollectionAssert.AreEqual(TRANSACTION_OUTPUT_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeTransactionOut()
        {
            var actual = DataEncoder.EncodeTxOutput(DataEncoder.DecodeTxOutput(TRANSACTION_OUTPUT_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(TRANSACTION_OUTPUT_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeTransaction()
        {
            var actual = DataEncoder.EncodeTransaction(TRANSACTION_1);
            CollectionAssert.AreEqual(TRANSACTION_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeTransaction()
        {
            var actual = DataEncoder.EncodeTransaction(DataEncoder.DecodeTransaction(TRANSACTION_1_BYTES.ToArray()));
            CollectionAssert.AreEqual(TRANSACTION_1_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeVersionPayloadWithoutRelay()
        {
            var actual = DataEncoder.EncodeVersionPayload(VERSION_PAYLOAD_1_NO_RELAY, withRelay: false);
            CollectionAssert.AreEqual(VERSION_PAYLOAD_1_NO_RELAY_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireEncodeVersionPayloadWithRelay()
        {
            var actual = DataEncoder.EncodeVersionPayload(VERSION_PAYLOAD_2_RELAY, withRelay: true);
            CollectionAssert.AreEqual(VERSION_PAYLOAD_2_RELAY_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeVersionPayloadWithoutRelay()
        {
            var actual = DataEncoder.EncodeVersionPayload(DataEncoder.DecodeVersionPayload(VERSION_PAYLOAD_1_NO_RELAY_BYTES.ToArray(), VERSION_PAYLOAD_1_NO_RELAY_BYTES.Count), withRelay: false);
            CollectionAssert.AreEqual(VERSION_PAYLOAD_1_NO_RELAY_BYTES.ToList(), actual.ToList());
        }

        [TestMethod]
        public void TestWireDecodeVersionPayloadWithRelay()
        {
            var actual = DataEncoder.EncodeVersionPayload(DataEncoder.DecodeVersionPayload(VERSION_PAYLOAD_2_RELAY_BYTES.ToArray(), VERSION_PAYLOAD_2_RELAY_BYTES.Count), withRelay: true);
            CollectionAssert.AreEqual(VERSION_PAYLOAD_2_RELAY_BYTES.ToList(), actual.ToList());
        }
    }
}
