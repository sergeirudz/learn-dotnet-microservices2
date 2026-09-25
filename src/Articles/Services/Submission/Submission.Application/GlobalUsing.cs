// Insights - global using :
// Benefits :
// - reduces boirleplate using statements across the project.
// - makes the files more readable 
// Drawbacks :
// - can lead to confusion about where the types are coming from.
// - can lead to conflicts if the same type name is defined in multiple namespaces (ex: ValidationMessages)

// Third-party libraries

global using MediatR;
global using FluentValidation;

// Internal libraries
global using Blocks.MediatR;
global using Articles.Abstractions;
global using Articles.Abstractions.Enums;

// Domain
global using Submission.Domain.Entities;
global using Submission.Domain.Enums;

// Application
global using Submission.Application.Features.Shared;

//Persistence
global using Submission.Persistence.Repositories;