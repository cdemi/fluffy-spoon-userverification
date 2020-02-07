using Bogus;
using demofluffyspoon.contracts;
using demofluffyspoon.contracts.Models;
using Orleans.Streams;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UserVerificationComponentTests
{
    public class UserVerificationTests: IClassFixture<ClusterFixture>, IAsyncObserver<UserVerificationEvent>
    {
        private readonly ClusterFixture _cluster;
        private UserVerificationEvent _userVerificationEvent;
        private readonly SemaphoreSlim _semaphore;
        private readonly Faker _faker = new Faker();
        
        public UserVerificationTests(ClusterFixture fixture)
        {
            _cluster = fixture;
            _userVerificationEvent = null;
            _semaphore = new SemaphoreSlim(0, 1);
        }

        [Fact]
        public async Task UserVerification_DuplicateEmail_NotProcessed()
        {
            Guid testGuid = Guid.NewGuid();
            int semaphoreTimeout = 1000;

            string testEmail = _faker.Internet.Email();
            UserRegisteredEvent userRegisteredEvent = new UserRegisteredEvent()
                {Email = testEmail, Name = _faker.Random.String2(5), Surname = _faker.Random.String2(5)};

            var streamProvider = _cluster.Cluster.Client.GetStreamProvider(Constants.StreamProviderName);
            var userRegistrationStream = streamProvider.GetStream<UserRegisteredEvent>(testGuid, nameof(UserRegisteredEvent));
            var userVerifiedStream = streamProvider.GetStream<UserVerificationEvent>(testGuid, nameof(UserVerificationEvent));
            
            await userVerifiedStream.SubscribeAsync(this);
            await userRegistrationStream.OnNextAsync(userRegisteredEvent);
            _semaphore.Wait(semaphoreTimeout);

            Assert.Equal(testEmail, _userVerificationEvent.Email);
            Assert.Equal(UserVerificationStatusEnum.Verified, _userVerificationEvent.Status);
            _userVerificationEvent = null;
            
            await userRegistrationStream.OnNextAsync(userRegisteredEvent);
            _semaphore.Wait(semaphoreTimeout);

            Assert.NotNull(_userVerificationEvent);
            Assert.Equal(testEmail, _userVerificationEvent.Email);
            Assert.Equal(UserVerificationStatusEnum.Duplicate, _userVerificationEvent.Status);
        }

        public Task OnNextAsync(UserVerificationEvent item, StreamSequenceToken token = null)
        {
            _userVerificationEvent = item;
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