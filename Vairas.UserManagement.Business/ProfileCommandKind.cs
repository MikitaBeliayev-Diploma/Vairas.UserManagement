using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

public enum ProfileCommandKind { CreateUser, UpdateUser, CreateStudent, CreateTeacher, CreateAdministrator, BootstrapAdministrator, Deactivate }
