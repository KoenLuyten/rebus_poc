using MediatR;

namespace RebusPoc.EventClient.Messaging;

/// <summary>Marker for requests that change state. <see cref="TransactionBehavior{TRequest,TResponse}"/> wraps them in a transaction.</summary>
public interface ICommand;

public interface ICommand<out TResponse> : IRequest<TResponse>, ICommand;

public interface IQuery<out TResponse> : IRequest<TResponse>;
