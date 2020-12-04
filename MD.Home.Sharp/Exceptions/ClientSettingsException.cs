using System;

namespace MD.Home.Sharp.Exceptions
{
    internal sealed class ClientSettingsException : Exception
    {
        public ClientSettingsException(string message) : base(message) { }
    }
}