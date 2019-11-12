using demofluffyspoon.contracts;
using demofluffyspoon.contracts.Grains;
using demofluffyspoon.contracts.Models;
using Orleans;
using Orleans.Streams;
using System;
using System.Threading.Tasks;

namespace fluffyspoon.userverification.Grains
{
    [ImplicitStreamSubscription(nameof(UserRegisteredEvent))]
    public class UserVerificationGrain : Grain, IUserVerificationGrain, IAsyncObserver<UserRegisteredEvent>
    {
        private IAsyncStream<UserVerifiedEvent> _userVerifiedStream;

        public override async Task OnActivateAsync()
        {
            var streamProvider = GetStreamProvider(Constants.StreamProviderName);

            _userVerifiedStream = streamProvider.GetStream<UserVerifiedEvent>(this.GetPrimaryKey(), nameof(UserVerifiedEvent));

            var userRegisteredStream = streamProvider.GetStream<UserRegisteredEvent>(this.GetPrimaryKey(), nameof(UserRegisteredEvent));
            await userRegisteredStream.SubscribeAsync(this);

            await base.OnActivateAsync();
        }

        public Task OnNextAsync(UserRegisteredEvent item, StreamSequenceToken token = null)
        {
            _userVerifiedStream.OnNextAsync(new UserVerifiedEvent()
            {
                Email = item.Email
            });

            return Task.CompletedTask;
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