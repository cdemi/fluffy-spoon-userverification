using demofluffyspoon.contracts;
using demofluffyspoon.contracts.Grains;
using demofluffyspoon.contracts.Models;
using fluffyspoon.userverification.States;
using GiG.Core.Data.KVStores.Abstractions;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Streams;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace fluffyspoon.userverification.Grains
{
    [ImplicitStreamSubscription(nameof(UserRegisteredEvent))]
    public class UserVerificationGrain : Grain<UserVerificationState>, IUserVerificationGrain,
        IAsyncObserver<UserRegisteredEvent>
    {
        private readonly IDataRetriever<HashSet<string>> _blacklistedEmails;
        private readonly ILogger<UserVerificationGrain> _logger;

        private IAsyncStream<UserVerificationEvent> _userVerificationStream;

        public UserVerificationGrain(IDataRetriever<HashSet<string>> blacklistedEmails,
            ILogger<UserVerificationGrain> logger)
        {
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

            if (State.IsAlreadyVerified)
            {
                @event.Status = UserVerificationStatusEnum.Duplicate;
            }

            if (_blacklistedEmails.Get().Contains(item.Email))
            {
                @event.Status = UserVerificationStatusEnum.Blocked;

                _logger.LogWarning("Blacklisted user {email}", item.Email);
            }

            await _userVerificationStream.OnNextAsync(@event);

            State.IsAlreadyVerified = true;
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