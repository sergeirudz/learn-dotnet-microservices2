// Internal libraries

global using Articles.Abstractions;
global using Articles.Abstractions.Enums;

// Domain
global using Submission.Domain.Entities;
global using Submission.Domain.Enums;
global using Submission.Domain.ValueObjects;

// This is needed so there is no conflict with System `File`
global using File = Submission.Domain.ValueObjects.File;