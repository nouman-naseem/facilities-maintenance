using Facilities.Core.Abstractions;
using Facilities.Core.Enums;

namespace Facilities.Tests.TestSupport;

public class TestCurrentUserContext : ICurrentUserContext
{
    public Guid UserId { get; set; }
    public Guid OrganisationId { get; set; }
    public UserRole Role { get; set; }
}
