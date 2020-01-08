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
    public class UserVerificationTests
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
            await _verificationCluster.StopSiloAsync(_verificationCluster.Primary);
            
            IUserRegistrationGrain userRegistrationGrain = _registrationCluster.Client.GetGrain<IUserRegistrationGrain>(_faker.Internet.Email());
            Guid? userRegistrationKey = await userRegistrationGrain.RegisterAsync(_faker.Random.String2(5), _faker.Random.String2(5));
            IUserRegistrationStatusGrain userRegistrationStatusGrain = _registrationStatusCluster.Client.GetGrain<IUserRegistrationStatusGrain>((Guid) userRegistrationKey);
            
            await AssertRegistrationState(userRegistrationStatusGrain, UserRegistrationStatusEnum.Pending);

            //Act
            await _verificationCluster.StartAdditionalSiloAsync();
            await _verificationCluster.WaitForLivenessToStabilizeAsync();
            
            //Assert
            await AssertRegistrationState(userRegistrationStatusGrain, UserRegistrationStatusEnum.Verified);
            
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
            
            IUserRegistrationGrain userRegistrationGrain = _registrationCluster.Client.GetGrain<IUserRegistrationGrain>(_faker.Internet.Email());
            Guid? userRegistrationKey = await userRegistrationGrain.RegisterAsync(_faker.Random.String2(5), _faker.Random.String2(5));
            IUserRegistrationStatusGrain userRegistrationStatusGrain = _registrationStatusCluster.Client.GetGrain<IUserRegistrationStatusGrain>((Guid) userRegistrationKey);

            await AssertRegistrationState(userRegistrationStatusGrain, UserRegistrationStatusEnum.Pending);

            //Act
            //Build Verification Service
            _verificationCluster = _testHelper.GenerateTestCluster<UserVerificationGrain>();
            await _verificationCluster.WaitForLivenessToStabilizeAsync();
            
            //Assert
            await AssertRegistrationState(userRegistrationStatusGrain, UserRegistrationStatusEnum.Verified);
            
            _registrationCluster.StopAllSilos();
            _registrationStatusCluster.StopAllSilos();
            _verificationCluster.StopAllSilos();
        }

        private async Task AssertRegistrationState(IUserRegistrationStatusGrain userRegistrationStatusGrain, UserRegistrationStatusEnum expectedStatus)
        {
            
//            DateTime endTime = DateTime.Now.AddSeconds(60);
//            DateTime currentTime;
            
//            do
//            {
//            Thread.Sleep(10000);
//                RegistrationStatusState registrationState = await userRegistrationStatusGrain.GetAsync();
//                currentTime = DateTime.Now;
//                await Task.Delay(200);
//            } while (!registrationState.Status.Equals(expectedStatus) && currentTime < endTime);
            
            await Task.Delay(2000);
            RegistrationStatusState registrationState = await userRegistrationStatusGrain.GetAsync();
            Assert.Equal(expectedStatus, registrationState.Status);
        }
        
    }
}