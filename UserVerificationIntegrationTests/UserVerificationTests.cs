using Bogus;
using demofluffyspoon.contracts.Grains;
using demofluffyspoon.contracts.Models;
using demofluffyspoon.registration.grains.Grains;
using fluffyspoon.userverification.Grains;
using Orleans.TestingHost;
using System;
using System.Threading.Tasks;
using Xunit;

namespace UserVerificationIntegrationTests
{
    public class UserVerificationTests: IDisposable
    {
        private readonly TestHelper _testHelper;
        private readonly TestCluster _registrationCluster;
        private readonly TestCluster _registrationStatusCluster;
        private TestCluster _verificationCluster;
        private readonly Faker _faker = new Faker();

        public UserVerificationTests()
        {
            _testHelper = new TestHelper();
            //Build Registration Services - register and get status
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
            
            //Arrange
            await _registrationCluster.WaitForLivenessToStabilizeAsync();
            await _registrationStatusCluster.WaitForLivenessToStabilizeAsync();
           
            _verificationCluster = _testHelper.GenerateTestCluster<UserVerificationGrain>();
            await _verificationCluster.WaitForLivenessToStabilizeAsync();
            
            IUserRegistrationGrain userRegistrationGrain = _registrationCluster.Client.GetGrain<IUserRegistrationGrain>(_faker.Internet.Email());
            Guid userRegistrationKey = (Guid) await userRegistrationGrain.RegisterAsync(_faker.Random.String2(5), _faker.Random.String2(5));
            
            await AssertRegistrationState(userRegistrationKey, UserRegistrationStatusEnum.Verified);
            
            await _verificationCluster.StopSiloAsync(_verificationCluster.Primary);
            
            IUserRegistrationGrain userRegistrationGrain1 = _registrationCluster.Client.GetGrain<IUserRegistrationGrain>(_faker.Internet.Email());
            Guid userRegistrationKey1 = (Guid) await userRegistrationGrain1.RegisterAsync(_faker.Random.String2(5), _faker.Random.String2(5));

            await AssertRegistrationState(userRegistrationKey1, UserRegistrationStatusEnum.Pending);

            //Act
            //Build Verification Service
            await _verificationCluster.StartAdditionalSiloAsync();
            await _verificationCluster.WaitForLivenessToStabilizeAsync();
            
            //Assert
            await AssertRegistrationState(userRegistrationKey1, UserRegistrationStatusEnum.Verified);
            
            _registrationCluster.StopAllSilos();
            _registrationStatusCluster.StopAllSilos();
            _verificationCluster.StopAllSilos();
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
            
            //Arrange
            await _registrationCluster.WaitForLivenessToStabilizeAsync();
            await _registrationStatusCluster.WaitForLivenessToStabilizeAsync();
            
            _verificationCluster = _testHelper.GenerateTestCluster<UserVerificationGrain>();
            await _verificationCluster.WaitForLivenessToStabilizeAsync();
            
            IUserRegistrationGrain userRegistrationGrain = _registrationCluster.Client.GetGrain<IUserRegistrationGrain>(_faker.Internet.Email());
            Guid userRegistrationKey = (Guid) await userRegistrationGrain.RegisterAsync(_faker.Random.String2(5), _faker.Random.String2(5));
            
            await AssertRegistrationState(userRegistrationKey, UserRegistrationStatusEnum.Verified);
            
            _verificationCluster.Dispose();
            
            IUserRegistrationGrain userRegistrationGrain1 = _registrationCluster.Client.GetGrain<IUserRegistrationGrain>(_faker.Internet.Email());
            Guid userRegistrationKey1 = (Guid) await userRegistrationGrain1.RegisterAsync(_faker.Random.String2(5), _faker.Random.String2(5));

            await AssertRegistrationState(userRegistrationKey1, UserRegistrationStatusEnum.Pending);

            //Act
            //Build Verification Service
            _verificationCluster = _testHelper.GenerateTestCluster<UserVerificationGrain>();
            await _verificationCluster.WaitForLivenessToStabilizeAsync();
            
            //Assert
            await AssertRegistrationState(userRegistrationKey1, UserRegistrationStatusEnum.Verified);
            
            _registrationCluster.StopAllSilos();
            _registrationStatusCluster.StopAllSilos();
            _verificationCluster.StopAllSilos();
        }

        private async Task AssertRegistrationState(Guid registrationKey, UserRegistrationStatusEnum expectedStatus)
        {
            IUserRegistrationStatusGrain userRegistrationStatusGrain =
                _registrationStatusCluster.Client.GetGrain<IUserRegistrationStatusGrain>(registrationKey);

            Assert.True(await TestHelper.Polly.ExecuteAsync(async () =>
            {
                RegistrationStatusState registrationState =
                    await userRegistrationStatusGrain.GetAsync();

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