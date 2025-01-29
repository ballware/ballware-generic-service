namespace Ballware.Generic.Service.Configuration;

public class ConfigurationException(string message, Exception? innerException = null)
    : Exception(message, innerException);