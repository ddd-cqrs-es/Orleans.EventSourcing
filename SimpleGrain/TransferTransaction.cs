﻿using System;
using System.Threading.Tasks;
using Orleans.EventSourcing.SimpleGrain.Events;
using Orleans.EventSourcing.SimpleInterface;
using Orleans.Providers;

namespace Orleans.EventSourcing.SimpleGrain
{
    [StorageProvider(ProviderName = "MongoDBStore")]
    public class TransferTransaction : EventSourcingGrain<TransferTransaction, TransferTransactionState>,
                                       ITransferTransaction
    {
        async Task ITransferTransaction.Initialize(Guid fromAccountId, Guid toAccountId, decimal amount)
        {
            if (this.State.Status == default(TransactionStatus))
            {
                var transactionInfo = new TransferTransactionInfo { FromAccountId = fromAccountId, ToAccountId = toAccountId, Amount = amount };
                await this.ApplyEvent(new TransferTransactionStartedEvent(this.GetPrimaryKey(), transactionInfo));
            }
        }
        async Task ITransferTransaction.ConfirmAccountValidatePassed()
        {
            if (this.State.Status == TransactionStatus.Started)
            {
                await this.ApplyEvent(new AccountValidatePassedEvent(this.GetPrimaryKey(), this.State.TransferTransactionInfo));
            }
        }

        async Task ITransferTransaction.ConfirmTransferOutPreparation()
        {
            if (this.State.Status == TransactionStatus.AccountValidateCompleted)
            {
                if (!this.State.TransferOutPreparationConfirmed)
                    await this.ApplyEvent(new TransferOutPreparationConfirmedEvent(this.GetPrimaryKey(), this.State.TransferTransactionInfo));
            }
        }

        async Task ITransferTransaction.ConfirmTransferInPreparation()
        {
            if (this.State.Status == TransactionStatus.AccountValidateCompleted)
            {
                if (!this.State.TransferInPreparationConfirmed)
                    await this.ApplyEvent(new TransferInPreparationConfirmedEvent(this.GetPrimaryKey(), this.State.TransferTransactionInfo));
            }
        }

        async Task ITransferTransaction.ConfirmTransferOut()
        {
            if (this.State.Status == TransactionStatus.PreparationCompleted)
            {
                if (!this.State.TransferOutConfirmed)
                    await this.ApplyEvent(new TransferOutConfirmedEvent(this.GetPrimaryKey(), this.State.TransferTransactionInfo));
            }
        }

        async Task ITransferTransaction.ConfirmTransferIn()
        {
            if (this.State.Status == TransactionStatus.PreparationCompleted)
            {
                if (!this.State.TransferInConfirmed)
                    await this.ApplyEvent(new TransferInConfirmedEvent(this.GetPrimaryKey(), this.State.TransferTransactionInfo));
            }
        }

        async Task ITransferTransaction.Cancel(TransactionFaileReason reason)
        {
            if (this.State.Status != TransactionStatus.Completed)
            {
                await this.ApplyEvent(new TransferCanceledEvent(this.GetPrimaryKey(), reason));
            }
        }
        Task<TransactionStatus> ITransferTransaction.GetStatus()
        {
            return Task.FromResult(this.State.Status);
        }

        Task<TransferTransactionInfo> ITransferTransaction.GetTransferTransactionInfo()
        {
            return Task.FromResult(this.State.TransferTransactionInfo);
        }

        #region event handlers
        private void Handle(TransferTransactionStartedEvent @event)
        {
            this.State.Status = TransactionStatus.Started;
            this.State.TransferTransactionInfo = @event.TransferTransactionInfo;
        }
        private void Handle(AccountValidatePassedEvent @event)
        {
            this.State.Status = TransactionStatus.AccountValidateCompleted;
            this.State.AccountValidatedAt = @event.UTCTimestamp;
        }
        private void Handle(TransferOutPreparationConfirmedEvent @event)
        {
            this.State.TransferOutPreparationConfirmed = true;
            this.State.TransferOutPreparationConfirmedAt = @event.UTCTimestamp;

            if (this.State.TransferInPreparationConfirmed)
                this.State.Status = TransactionStatus.PreparationCompleted;
        }
        private void Handle(TransferInPreparationConfirmedEvent @event)
        {
            this.State.TransferInPreparationConfirmed = true;
            this.State.TransferInPreparationConfirmedAt = @event.UTCTimestamp;
            if (this.State.TransferOutPreparationConfirmed)
                this.State.Status = TransactionStatus.PreparationCompleted;
        }
        private void Handle(TransferOutConfirmedEvent @event)
        {
            this.State.TransferOutConfirmed = true;
            this.State.TransferOutConfirmedAt = @event.UTCTimestamp;
            if (this.State.TransferInConfirmed)
            {
                this.State.Status = TransactionStatus.Completed;
                this.State.WriteStateAsync();
            }
        }
        private void Handle(TransferInConfirmedEvent @event)
        {
            this.State.TransferInConfirmed = true;
            this.State.TransferInConfirmedAt = @event.UTCTimestamp;
            if (this.State.TransferOutConfirmed)
            {
                this.State.Status = TransactionStatus.Completed;
                this.State.WriteStateAsync();
            }
        }
        private void Handle(TransferCanceledEvent @event)
        {
            this.State.TransferCancelAt = @event.UTCTimestamp;
            this.State.Status = TransactionStatus.Canceled;
        }
        #endregion
    }

    public class TransferTransactionState : EventSourcingState
    {
        public TransactionStatus Status { get; set; }
        public bool TransferOutPreparationConfirmed { get; set; }
        public bool TransferInPreparationConfirmed { get; set; }
        public bool TransferOutConfirmed { get; set; }
        public bool TransferInConfirmed { get; set; }
        public DateTime AccountValidatedAt { get; set; }
        public DateTime TransferOutPreparationConfirmedAt { get; set; }
        public DateTime TransferInPreparationConfirmedAt { get; set; }
        public DateTime TransferOutConfirmedAt { get; set; }
        public DateTime TransferInConfirmedAt { get; set; }
        public DateTime TransferCancelAt { get; set; }
        public TransferTransactionInfo TransferTransactionInfo { get; set; }
    }
}
