using System;

namespace DemoFluffySpoon.UserVerification.States
{
    [Serializable]
    public class UserVerificationState
    {
        public bool IsAlreadyVerified { get; set; }
    }
}