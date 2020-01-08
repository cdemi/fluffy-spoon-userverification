using System;

namespace fluffyspoon.userverification.States
{
    [Serializable]
    public class UserVerificationState
    {
        public bool IsAlreadyVerified { get; set; }
    }
}