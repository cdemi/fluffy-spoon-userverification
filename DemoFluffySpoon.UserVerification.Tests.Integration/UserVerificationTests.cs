using System;
using System.Threading.Tasks;
using Bogus;
using DemoFluffySpoon.Contracts.Grains;
using DemoFluffySpoon.Contracts.Models;
using DemoFluffySpoon.Registration.Grains.Grains;
using DemoFluffySpoon.UserVerification.Grains;
using Orleans.TestingHost;
using Xunit;

namespace DemoFluffySpoon.UserVerification.Tests.Integration
{
    [Trait("Category", "Integration")]
    public class UserVerificationTests : IDisposable
    {
        private readonly TestHelper _testHelper;
        private readonly TestCluster _registrationCluster;
        private readonly TestCluster _registrationStatusCluster;
        private readonly Faker _faker = new Faker();

        private TestCluster _verificationCluster;

        public UserVerificationTests()
        {
            _testHelper = new TestHelper();

            // Build Registration Services - register and get status
            _registrationCluster = _testHelper.GenerateTestCluster<UserRegistrationGrain>();
            _registrationStatusCluster = _testHelper.GenerateTestCluster<UserRegistrationStatusGrain>();
        }

        [Fact]
        public async Task UserVerification_SiloDown_UserRegisteredWhenRestarted()
        {
            /*
             * Enable Verification service
             * Disable verification service
             * Send Registration
             * Assert that the state is pending
             * Switch on verification service
             * Assert that the state is verified
             */

            // Arrange
            await _registrationCluster.WaitForLivenessToStabilizeAsync();
            await _registrationStatusCluster.WaitForLivenessToStabilizeAsync();

            _verificationCluster = _testHelper.GenerateTestCluster<UserVerificationGrain>();
            await _verificationCluster.WaitForLivenessToStabilizeAsync();

            var userRegistrationGrain =
                _registrationCluster.Client.GetGrain<IUserRegistrationGrain>(_faker.Internet.Email());
            var userRegistrationKey =
                await userRegistrationGrain.RegisterAsync(_faker.Random.String2(5), _faker.Random.String2(5));

            await AssertRegistrationState(userRegistrationKey, UserRegistrationStatusEnum.Verified);

            await _verificationCluster.StopSiloAsync(_verificationCluster.Primary);

            var userRegistrationGrain1 =
                _registrationCluster.Client.GetGrain<IUserRegistrationGrain>(_faker.Internet.Email());
            var userRegistrationKey1 =
                await userRegistrationGrain1.RegisterAsync(_faker.Random.String2(5), _faker.Random.String2(5));

            await AssertRegistrationState(userRegistrationKey1, UserRegistrationStatusEnum.Pending);

            // Act
            // Build Verification Service
            await _verificationCluster.StartAdditionalSiloAsync();
            await _verificationCluster.WaitForLivenessToStabilizeAsync();

            // Assert
            await AssertRegistrationState(userRegistrationKey1, UserRegistrationStatusEnum.Verified);

            await _registrationCluster.StopAllSilosAsync();
            await _registrationStatusCluster.StopAllSilosAsync();
            await _verificationCluster.StopAllSilosAsync();
        }

        [Fact]
        public async Task UserVerification_ClusterDown_UserRegisteredWhenRestarted()
        {
            /*
             * Send Registration
             * Assert that the state is pending
             * Switch on verification service
             * Assert that the state is verified
             */

            // Arrange
            await _registrationCluster.WaitForLivenessToStabilizeAsync();
            await _registrationStatusCluster.WaitForLivenessToStabilizeAsync();

            _verificationCluster = _testHelper.GenerateTestCluster<UserVerificationGrain>();
            await _verificationCluster.WaitForLivenessToStabilizeAsync();

            var userRegistrationGrain =
                _registrationCluster.Client.GetGrain<IUserRegistrationGrain>(_faker.Internet.Email());
            var userRegistrationKey =
                await userRegistrationGrain.RegisterAsync(_faker.Random.String2(5), _faker.Random.String2(5));

            await AssertRegistrationState(userRegistrationKey, UserRegistrationStatusEnum.Verified);

            _verificationCluster.Dispose();

            var userRegistrationGrain1 =
                _registrationCluster.Client.GetGrain<IUserRegistrationGrain>(_faker.Internet.Email());
            var userRegistrationKey1 =
                await userRegistrationGrain1.RegisterAsync(_faker.Random.String2(5), _faker.Random.String2(5));

            await AssertRegistrationState(userRegistrationKey1, UserRegistrationStatusEnum.Pending);

            // Act
            // Build Verification Service
            _verificationCluster = _testHelper.GenerateTestCluster<UserVerificationGrain>();
            await _verificationCluster.WaitForLivenessToStabilizeAsync();

            // Assert
            await AssertRegistrationState(userRegistrationKey1, UserRegistrationStatusEnum.Verified);

            await _registrationCluster.StopAllSilosAsync();
            await _registrationStatusCluster.StopAllSilosAsync();
            await _verificationCluster.StopAllSilosAsync();
        }

        private async Task AssertRegistrationState(Guid? registrationKey, UserRegistrationStatusEnum expectedStatus)
        {
            Assert.NotNull(registrationKey);
            var userRegistrationStatusGrain =
                _registrationStatusCluster.Client.GetGrain<IUserRegistrationStatusGrain>(registrationKey.Value);

            Assert.True(await TestHelper.Polly.ExecuteAsync(async () =>
            {
                var registrationState = await userRegistrationStatusGrain.GetAsync();

                return registrationState.Status.Equals(expectedStatus);
            }));
        }

        public void Dispose()
        {
            _registrationCluster.Dispose();
            _registrationStatusCluster.Dispose();
            _verificationCluster.Dispose();
        }
    }
}