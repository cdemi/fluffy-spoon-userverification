using demofluffyspoon.contracts;
using demofluffyspoon.contracts.Models;
using Orleans.Streams;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UserVerificationComponentTests
{
    public class UserVerificationTests: IClassFixture<ClusterFixture>, IAsyncObserver<UserVerifiedEvent>
    {
        private readonly ClusterFixture _cluster;
        private UserVerifiedEvent _userVerifiedEvent;
        private readonly SemaphoreSlim _semaphore;
        
        public UserVerificationTests(ClusterFixture fixture)
        {
            _cluster = fixture;
            _userVerifiedEvent = null;
            _semaphore = new SemaphoreSlim(0, 1);
        }

        [Fact]
        public async Task UserVerification_DuplicateEmail_NotProcessed()
        {
            Guid testGuid = Guid.NewGuid();


            var streamProvider = _cluster.Cluster.Client.GetStreamProvider(Constants.StreamProviderName);
            var userRegistrationStream = streamProvider.GetStream<UserRegisteredEvent>(testGuid, nameof(UserRegisteredEvent));
            var userVerifiedStream = streamProvider.GetStream<UserVerifiedEvent>(testGuid, nameof(UserVerifiedEvent));


            await userVerifiedStream.SubscribeAsync(this);
            await userRegistrationStream.OnNextAsync(new UserRegisteredEvent() {Email = "testing@test.com", Name = "test1", Surname = "test2"});
            _semaphore.Wait(10000);

            Assert.Equal("testing@test.com", _userVerifiedEvent.Email);
            _userVerifiedEvent = null;
            
            await userRegistrationStream.OnNextAsync(new UserRegisteredEvent() {Email = "testing@test.com", Name = "test1", Surname = "test2"});

            _semaphore.Wait(10000);

            Assert.Equal("testing@test.com", _userVerifiedEvent.Email);
        }

        public Task OnNextAsync(UserVerifiedEvent item, StreamSequenceToken token = null)
        {
            _userVerifiedEvent = item;
            _semaphore.Release();
            
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