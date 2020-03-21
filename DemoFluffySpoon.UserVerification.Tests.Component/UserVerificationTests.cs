using System;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using DemoFluffySpoon.Contracts;
using DemoFluffySpoon.Contracts.Models;
using Orleans.Streams;
using Xunit;

namespace DemoFluffySpoon.UserVerification.Tests.Component
{
    [Trait("Category", "Component")]
    public class UserVerificationTests : IClassFixture<ClusterFixture>, IAsyncObserver<UserVerificationEvent>
    {
        private readonly ClusterFixture _cluster;
        private readonly SemaphoreSlim _semaphore;
        private readonly Faker _faker = new Faker();

        private UserVerificationEvent _userVerificationEvent;

        public UserVerificationTests(ClusterFixture fixture)
        {
            _cluster = fixture;
            _userVerificationEvent = null;
            _semaphore = new SemaphoreSlim(0, 1);
        }

        [Fact]
        public async Task UserVerification_DuplicateEmail_NotProcessed()
        {
            var testGuid = Guid.NewGuid();
            const int semaphoreTimeout = 1000;

            var testEmail = _faker.Internet.Email();
            var userRegisteredEvent = new UserRegisteredEvent()
                {Email = testEmail, Name = _faker.Random.String2(5), Surname = _faker.Random.String2(5)};

            var streamProvider = _cluster.Cluster.Client.GetStreamProvider(Constants.StreamProviderName);
            var userRegistrationStream =
                streamProvider.GetStream<UserRegisteredEvent>(testGuid, nameof(UserRegisteredEvent));
            var userVerifiedStream =
                streamProvider.GetStream<UserVerificationEvent>(testGuid, nameof(UserVerificationEvent));

            await userVerifiedStream.SubscribeAsync(this);
            await userRegistrationStream.OnNextAsync(userRegisteredEvent);
            await _semaphore.WaitAsync(semaphoreTimeout);

            Assert.Equal(testEmail, _userVerificationEvent.Email);
            Assert.Equal(UserVerificationStatusEnum.Verified, _userVerificationEvent.Status);
            _userVerificationEvent = null;

            await userRegistrationStream.OnNextAsync(userRegisteredEvent);
            await _semaphore.WaitAsync(semaphoreTimeout);

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