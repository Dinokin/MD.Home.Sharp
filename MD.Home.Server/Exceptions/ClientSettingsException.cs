using System;

namespace MD.Home.Server.Exceptions
{
    public class ClientSettingsException : Exception
    {
        public ClientSettingsException(string message) : base(message) { }
    }
}