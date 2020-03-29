using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DemoFluffySpoon.Contracts;
using DemoFluffySpoon.Contracts.Grains;
using DemoFluffySpoon.Contracts.Models;
using DemoFluffySpoon.UserVerification.States;
using GiG.Core.Data.KVStores.Abstractions;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;

namespace DemoFluffySpoon.UserVerification.Grains
{
    [ImplicitStreamSubscription(nameof(UserRegisteredEvent))]
    public class UserVerificationGrain : Grain, IUserVerificationGrain, IAsyncObserver<UserRegisteredEvent>
    {
        private readonly IPersistentState<UserVerificationState> _verificationState;
        private readonly IDataRetriever<HashSet<string>> _blacklistedEmails;
        private readonly ILogger<UserVerificationGrain> _logger;

        private IAsyncStream<UserVerificationEvent> _userVerificationStream;

        public UserVerificationGrain([PersistentState(nameof(UserVerificationState))]
            IPersistentState<UserVerificationState> verificationState,
            IDataRetriever<HashSet<string>> blacklistedEmails,
            ILogger<UserVerificationGrain> logger)
        {
            _verificationState = verificationState;
            _blacklistedEmails = blacklistedEmails;
            _logger = logger;
        }

        public override async Task OnActivateAsync()
        {
            var streamProvider = GetStreamProvider(Constants.StreamProviderName);

            // Producer
            _userVerificationStream =
                streamProvider.GetStream<UserVerificationEvent>(this.GetPrimaryKey(), nameof(UserVerificationEvent));

            // Consumer
            var userRegisteredStream =
                streamProvider.GetStream<UserRegisteredEvent>(this.GetPrimaryKey(), nameof(UserRegisteredEvent));
            await userRegisteredStream.SubscribeAsync(this);

            await base.OnActivateAsync();
        }

        public async Task OnNextAsync(UserRegisteredEvent item, StreamSequenceToken token = null)
        {
            var @event = new UserVerificationEvent()
            {
                Email = item.Email,
                Status = UserVerificationStatusEnum.Verified
            };

            if (_verificationState.State.IsAlreadyVerified)
            {
                @event.Status = UserVerificationStatusEnum.Duplicate;
            }

            if (_blacklistedEmails.Get().Contains(item.Email))
            {
                @event.Status = UserVerificationStatusEnum.Blocked;

                _logger.LogWarning("Blacklisted user {email}", item.Email);
            }

            await _userVerificationStream.OnNextAsync(@event);

            _verificationState.State.IsAlreadyVerified = true;
            await _verificationState.WriteStateAsync();
        }

        public Task OnCompletedAsync()
        {
            return Task.CompletedTask;
        }

        public Task OnErrorAsync(Exception ex)
        {
            return Task.CompletedTask;
        }
    }
}