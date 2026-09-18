namespace Facilities.Core.Exceptions;

/// <summary>Base type for business-rule violations raised by the domain/service layer.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public sealed class InvalidStateTransitionException : DomainException
{
    public InvalidStateTransitionException(string message) : base(message) { }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }
}

public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base(message) { }
}
