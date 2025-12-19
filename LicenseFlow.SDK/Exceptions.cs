using System;

namespace LicenseFlow.SDK
{
    public class LicenseFlowException : Exception
    {
        public string Code { get; }
        public int Status { get; }

        public LicenseFlowException(string message, string code, int status) : base(message)
        {
            Code = code;
            Status = status;
        }
    }

    public class RateLimitException : LicenseFlowException
    {
        public RateLimitException(string message) : base(message, "RATE_LIMIT_EXCEEDED", 429) { }
    }

    public class InvalidLicenseException : LicenseFlowException
    {
        public InvalidLicenseException(string message, int status) : base(message, "INVALID_LICENSE", status) { }
    }
}
