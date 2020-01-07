using demofluffyspoon.contracts;
using demofluffyspoon.contracts.Grains;
using demofluffyspoon.contracts.Models;
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
    public class UserVerificationGrain : Grain, IUserVerificationGrain, IAsyncObserver<UserRegisteredEvent>
    {
        private readonly IDataRetriever<HashSet<string>> _blacklistedEmails;
        private readonly ILogger<UserVerificationGrain> _logger;

        private IAsyncStream<UserVerifiedEvent> _userVerifiedStream;

        public UserVerificationGrain(IDataRetriever<HashSet<string>> blacklistedEmails, ILogger<UserVerificationGrain> logger)
        {
            _blacklistedEmails = blacklistedEmails;
            _logger = logger;
        }

        public override async Task OnActivateAsync()
        {
            var streamProvider = GetStreamProvider(Constants.StreamProviderName);

            // Producer
            _userVerifiedStream = streamProvider.GetStream<UserVerifiedEvent>(this.GetPrimaryKey(), nameof(UserVerifiedEvent));

            // Consumer
            var userRegisteredStream = streamProvider.GetStream<UserRegisteredEvent>(this.GetPrimaryKey(), nameof(UserRegisteredEvent));
            await userRegisteredStream.SubscribeAsync(this);

            await base.OnActivateAsync();
        }

        public async Task OnNextAsync(UserRegisteredEvent item, StreamSequenceToken token = null)
        {
            if (_blacklistedEmails.Get().Contains(item.Email))
            {
                _logger.LogWarning("Blacklisted user {email}", item.Email);

                return;
            }
            
            await _userVerifiedStream.OnNextAsync(new UserVerifiedEvent { Email = item.Email });
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