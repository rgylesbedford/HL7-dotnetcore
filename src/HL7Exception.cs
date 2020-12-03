﻿using System;

namespace HL7.Dotnetcore
{
    public class HL7Exception : Exception
    {
        public const string REQUIRED_FIELD_MISSING = "Validation Error - Required field missing in message";
        public const string UNSUPPORTED_MESSAGE_TYPE = "Validation Error - Message Type not supported by this implementation";
        public const string BAD_MESSAGE = "Validation Error - Bad Message";
        public const string PARSING_ERROR = "Parsing Error";
        public const string SERIALIZATION_ERROR = "Serialization Error";

        public string ErrorCode { get; set; }

        public HL7Exception(string message) : base(message)
        { }

        public HL7Exception(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        public override string ToString()
        {
            return ErrorCode + " : " + Message;
        }
    }
}
