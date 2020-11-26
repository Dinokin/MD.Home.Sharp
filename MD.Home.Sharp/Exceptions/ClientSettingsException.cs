using System;

namespace MD.Home.Sharp.Exceptions
{
    public class ClientSettingsException : Exception
    {
        public ClientSettingsException(string message) : base(message) { }
    }
}