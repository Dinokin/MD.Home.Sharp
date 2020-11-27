using System;

namespace MD.Home.Sharp.Exceptions
{
    internal class ClientSettingsException : Exception
    {
        public ClientSettingsException(string message) : base(message) { }
    }
}