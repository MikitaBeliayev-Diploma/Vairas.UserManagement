using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Encodings.Web;
using FluentValidation;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public sealed class ProfileCommandValidator : AbstractValidator<ProfileCommand>
{
    public ProfileCommandValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        When(x => x.Kind is ProfileCommandKind.CreateUser or ProfileCommandKind.UpdateUser, () =>
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100).Must(x => !string.IsNullOrWhiteSpace(x));
            RuleFor(x => x.LastName).NotEmpty().MaximumLength(100).Must(x => !string.IsNullOrWhiteSpace(x));
            RuleFor(x => x.EmailAddress).MaximumLength(254).EmailAddress().When(x => x.EmailAddress is not null);
            RuleFor(x => x.PhoneNumber).MaximumLength(32).Must(x => !string.IsNullOrWhiteSpace(x)).When(x => x.PhoneNumber is not null);
        });
        When(x => x.Kind is ProfileCommandKind.UpdateUser or ProfileCommandKind.Deactivate, () =>
            RuleFor(x => x.ExpectedRevision).NotNull().GreaterThan(0));
        When(x => x.Kind is ProfileCommandKind.Deactivate or ProfileCommandKind.BootstrapAdministrator, () =>
            RuleFor(x => x.TargetUserId).NotNull().NotEqual(Guid.Empty));
        When(x => x.Kind is ProfileCommandKind.CreateTeacher or ProfileCommandKind.CreateAdministrator or ProfileCommandKind.BootstrapAdministrator, () =>
            RuleFor(x => x.SchoolId).NotNull().NotEqual(Guid.Empty));
        When(x => x.Kind is ProfileCommandKind.CreateTeacher or ProfileCommandKind.CreateAdministrator, () =>
            RuleFor(x => x.InvitationId).NotNull().NotEqual(Guid.Empty));
        RuleFor(x => x.ConfirmationId).NotEqual(Guid.Empty).When(x => x.ConfirmationId.HasValue);
        RuleFor(x => x.Reason).MaximumLength(1000);
        When(x => x.Kind is not (ProfileCommandKind.Deactivate or ProfileCommandKind.BootstrapAdministrator), () =>
            RuleFor(x => x.TargetUserId).Null());
        When(x => x.Kind == ProfileCommandKind.CreateUser, () => RuleFor(x => x.ExpectedRevision).Null());
        When(x => x.Kind == ProfileCommandKind.CreateStudent, () =>
        {
            RuleFor(x => x.SchoolId).Null();
            RuleFor(x => x.InvitationId).Null();
        });
    }
}
