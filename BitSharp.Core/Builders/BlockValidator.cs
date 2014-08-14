using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Rules;
using BitSharp.Core.Storage;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    internal class BlockValidator : IDisposable
    {
        private readonly Logger logger;
        private readonly CoreStorage coreStorage;
        private readonly IBlockchainRules rules;

        private readonly ParallelConsumer<TxWithPrevOutputKeys> txLoader;
        private readonly ParallelConsumer<TxWithPrevOutputs> txValidator;
        private readonly ParallelConsumer<TxInputWithPrevOutput> scriptValidator;

        private ConcurrentDictionary<UInt256, Transaction> txCache;
        private BlockingQueue<TxWithPrevOutputKeys> pendingTxes;
        private BlockingQueue<TxWithPrevOutputs> loadedTxes;
        private BlockingQueue<TxInputWithPrevOutput> loadedTxInputs;
        private IDisposable txLoaderStopper;
        private IDisposable txValidatorStopper;
        private IDisposable scriptValidatorStopper;
        private ConcurrentBag<Exception> txLoaderExceptions;
        private ConcurrentBag<Exception> txValidatorExceptions;
        private ConcurrentBag<Exception> scriptValidatorExceptions;

        public BlockValidator(CoreStorage coreStorage, IBlockchainRules rules, Logger logger)
        {
            this.logger = logger;
            this.coreStorage = coreStorage;
            this.rules = rules;

            this.txLoader = new ParallelConsumer<TxWithPrevOutputKeys>("ChainStateBuilder.TxLoader", logger);
            this.txValidator = new ParallelConsumer<TxWithPrevOutputs>("ChainStateBuilder.TxValidator", logger);
            this.scriptValidator = new ParallelConsumer<TxInputWithPrevOutput>("ChainStateBuilder.ScriptValidator", logger);
        }

        public void Dispose()
        {
            new IDisposable[]
            {
                this.txLoader,
                this.txValidator,
                this.scriptValidator
            }.DisposeList();
        }

        public ConcurrentBag<Exception> TxLoaderExceptions { get { return this.txLoaderExceptions; } }

        public ConcurrentBag<Exception> TxValidatorExceptions { get { return this.txValidatorExceptions; } }

        public ConcurrentBag<Exception> ScriptValidatorExceptions { get { return this.scriptValidatorExceptions; } }

        public IDisposable StartValidation(ChainedHeader chainedHeader)
        {
            this.txCache = new ConcurrentDictionary<UInt256, Transaction>();

            this.pendingTxes = new BlockingQueue<TxWithPrevOutputKeys>();
            this.loadedTxes = new BlockingQueue<TxWithPrevOutputs>();
            this.loadedTxInputs = new BlockingQueue<TxInputWithPrevOutput>();

            this.txLoaderExceptions = new ConcurrentBag<Exception>();
            this.txValidatorExceptions = new ConcurrentBag<Exception>();
            this.scriptValidatorExceptions = new ConcurrentBag<Exception>();

            this.txLoaderStopper = StartTxLoader();
            this.txValidatorStopper = StartTxValidator(chainedHeader);
            this.scriptValidatorStopper = StartScriptValidator();

            return new Stopper(this);
        }

        public void AddPendingTx(TxWithPrevOutputKeys pendingTx)
        {
            this.pendingTxes.Add(pendingTx);
        }

        public void CompleteAdding()
        {
            this.pendingTxes.CompleteAdding();
        }

        public void WaitToComplete()
        {
            this.txLoader.WaitToComplete();
            this.txValidator.WaitToComplete();
            this.scriptValidator.WaitToComplete();
        }

        private void StopValidation()
        {
            this.pendingTxes.CompleteAdding();
            this.loadedTxes.CompleteAdding();
            this.loadedTxInputs.CompleteAdding();

            new IDisposable[]
            {
                this.txLoaderStopper,
                this.txValidatorStopper,
                this.scriptValidatorStopper,
                this.pendingTxes,
                this.loadedTxes,
                this.loadedTxInputs
            }.DisposeList();

            this.txCache = null;
            this.pendingTxes = null;
            this.loadedTxes = null;
            this.loadedTxInputs = null;
            this.txLoaderStopper = null;
            this.txValidatorStopper = null;
            this.scriptValidatorStopper = null;
            this.txLoaderExceptions = null;
            this.txValidatorExceptions = null;
            this.scriptValidatorExceptions = null;
        }

        private IDisposable StartTxLoader()
        {
            return this.txLoader.Start(pendingTxes.GetConsumingEnumerable(),
                pendingTx =>
                {
                    if (this.rules.BypassValidation)
                        return;

                    var loadedTx = LoadPendingTx(pendingTx, txCache);
                    if (loadedTx != null)
                        loadedTxes.Add(loadedTx);
                },
                () => loadedTxes.CompleteAdding());
        }

        private IDisposable StartTxValidator(ChainedHeader chainedHeader)
        {
            return this.txValidator.Start(loadedTxes.GetConsumingEnumerable(),
                loadedTx =>
                {
                    if (!this.rules.IgnoreScripts)
                    {
                        var transaction = loadedTx.Transaction;
                        var txIndex = loadedTx.TxIndex;
                        var prevTxOutputs = loadedTx.PrevTxOutputs;

                        if (txIndex > 0)
                        {
                            for (var inputIndex = 0; inputIndex < transaction.Inputs.Length; inputIndex++)
                            {
                                var txInput = transaction.Inputs[inputIndex];
                                var prevTxOutput = prevTxOutputs[inputIndex];

                                var txInputWithPrevOutput = new TxInputWithPrevOutput(chainedHeader, transaction, txIndex, txInput, inputIndex, prevTxOutput);
                                loadedTxInputs.Add(txInputWithPrevOutput);
                            }
                        }
                    }

                    ValidateTransaction(loadedTx);
                },
                () => loadedTxInputs.CompleteAdding());
        }

        private IDisposable StartScriptValidator()
        {
            return this.scriptValidator.Start(loadedTxInputs.GetConsumingEnumerable(),
                loadedTxInput =>
                {
                    ValidateTxInput(loadedTxInput);
                },
                () => { });
        }

        private TxWithPrevOutputs LoadPendingTx(TxWithPrevOutputKeys pendingTx, ConcurrentDictionary<UInt256, Transaction> txCache)
        {
            try
            {
                var txIndex = pendingTx.TxIndex;
                var transaction = pendingTx.Transaction;
                var chainedHeader = pendingTx.ChainedHeader;
                var spentTxes = pendingTx.PrevOutputTxKeys;

                var prevTxOutputs = ImmutableArray.CreateBuilder<TxOutput>(transaction.Inputs.Length);

                // load previous transactions for each input, unless this is a coinbase transaction
                if (txIndex > 0)
                {
                    for (var inputIndex = 0; inputIndex < transaction.Inputs.Length; inputIndex++)
                    {
                        var input = transaction.Inputs[inputIndex];

                        Transaction cachedPrevTx;
                        if (txCache.TryGetValue(input.PreviousTxOutputKey.TxHash, out cachedPrevTx))
                        {
                            var prevTxOutput = cachedPrevTx.Outputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()];
                            prevTxOutputs.Add(prevTxOutput);
                        }
                        else
                        {
                            var spentTx = spentTxes[inputIndex];

                            Transaction prevTx;
                            if (this.coreStorage.TryGetTransaction(spentTx.BlockHash, spentTx.TxIndex, out prevTx))
                            {
                                if (input.PreviousTxOutputKey.TxHash != prevTx.Hash)
                                    throw new Exception("TODO");

                                txCache.TryAdd(prevTx.Hash, prevTx);

                                var prevTxOutput = prevTx.Outputs[input.PreviousTxOutputKey.TxOutputIndex.ToIntChecked()];
                                prevTxOutputs.Add(prevTxOutput);
                            }
                            else
                            {
                                throw new Exception("TODO");
                            }
                        }
                    }

                    Debug.Assert(prevTxOutputs.Count == transaction.Inputs.Length);
                }

                var txWithPrevOutputs = new TxWithPrevOutputs(txIndex, transaction, chainedHeader, prevTxOutputs.ToImmutableArray());
                return txWithPrevOutputs;
            }
            catch (Exception e)
            {
                this.txLoaderExceptions.Add(e);
                //TODO
                return null;
            }
        }

        private void ValidateTransaction(TxWithPrevOutputs loadedTx)
        {
            try
            {
                var chainedHeader = loadedTx.ChainedHeader;
                var transaction = loadedTx.Transaction;
                var txIndex = loadedTx.TxIndex;
                var prevTxOutputs = loadedTx.PrevTxOutputs;

                this.rules.ValidateTransaction(chainedHeader, transaction, txIndex, prevTxOutputs);
            }
            catch (Exception e)
            {
                this.txValidatorExceptions.Add(e);
            }
        }

        private void ValidateTxInput(TxInputWithPrevOutput loadedTxInput)
        {
            try
            {
                this.rules.ValidationTransactionScript(loadedTxInput.ChainedHeader, loadedTxInput.Transaction, loadedTxInput.TxIndex, loadedTxInput.TxInput, loadedTxInput.InputIndex, loadedTxInput.PrevTxOutput);
            }
            catch (Exception e)
            {
                this.scriptValidatorExceptions.Add(e);
            }
        }

        private sealed class Stopper : IDisposable
        {
            private readonly BlockValidator blockValidator;

            public Stopper(BlockValidator blockValidator)
            {
                this.blockValidator = blockValidator;
            }

            public void Dispose()
            {
                this.blockValidator.StopValidation();
            }
        }
    }
}
