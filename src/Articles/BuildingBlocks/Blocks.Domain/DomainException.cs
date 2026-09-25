namespace Blocks.Domain;

public class DomainException(string message, Exception? innException = null)
    : Exception(message, innException)
{
}