﻿using System;
using System.Collections.Generic;
using System.Reflection;
using cdmdotnet.Logging;
using cdmdotnet.Logging.Configuration;
using cdmdotnet.StateManagement;
using cdmdotnet.StateManagement.Threaded;
using Cqrs.Authentication;
using Cqrs.Bus;
using Cqrs.Commands;
using Cqrs.Domain;
using Cqrs.Domain.Factories;
using Cqrs.Events;

namespace Cqrs.Configuration
{
	/// <summary>
	/// A sample runtime to use in proof of concept projects to get something running very quickly. Doesn't save anything. All data is lost when recycled and may cause terrible memory usage.
	/// </summary>
	/// <typeparam name="TAuthenticationToken">The <see cref="Type"/> of authentication token.</typeparam>
	/// <typeparam name="TCommandHanderOrEventHandler">The <see cref="Type"/> of any <see cref="ICommandHandle"/> or <see cref="IEventHandler"/>.</typeparam>
	public class SampleRuntime<TAuthenticationToken, TCommandHanderOrEventHandler> : IDisposable
	{
		/// <summary>
		/// Instaiance a new instance of the <see cref="SampleRuntime{TAuthenticationToken,TCommandHanderOrEventHandler}"/>
		/// </summary>
		public SampleRuntime()
		{
			MockDependencyResolver.Start();
			new BusRegistrar(DependencyResolver.Current)
				.Register(typeof(TCommandHanderOrEventHandler));
		}

		/// <summary>
		/// Prints out the statistics of this run such as the number of event raised to the <see cref="Console"/>.
		/// </summary>
		public void PrintStatsticsToConsole()
		{
			var inProcStore = DependencyResolver.Current.Resolve<IEventStore<TAuthenticationToken>>() as InProcessEventStore<TAuthenticationToken>;
			if (inProcStore != null)
			{
				var inMemoryDb = typeof(InProcessEventStore<TAuthenticationToken>).GetProperty("InMemoryDb", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inProcStore, null) as IDictionary<Guid, IList<IEvent<TAuthenticationToken>>>;
				Console.WriteLine("{0:N0} event{1} {2} raised.", inMemoryDb.Count, inMemoryDb.Count == 1 ? null : "s", inMemoryDb.Count == 1 ? "was" : "were");
			}
		}

		#region Implementation of IDisposable

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			var mockDependencyResolver = (MockDependencyResolver)DependencyResolver.Current;
			mockDependencyResolver.Dispose();
		}

		#endregion

		class MockDependencyResolver
			: DependencyResolver
			, IDisposable
		{
			private IContextItemCollectionFactory ContextFactory { get; set; }

			private ICorrelationIdHelper CorrelationIdHelper { get; set; }

			private IConfigurationManager ConfigurationManager { get; set; }

			private ILogger Logger { get; set; }

			private IEventStore<TAuthenticationToken> EventStore { get; set; }

			private InProcessBus<TAuthenticationToken> Bus { get; set; }

			private IAggregateRepository<TAuthenticationToken> AggregateRepository { get; set; }

			static MockDependencyResolver()
			{
				Current = new MockDependencyResolver();
			}

			public static void Start() { }

			private MockDependencyResolver()
			{
				ContextFactory = new ThreadedContextItemCollectionFactory();
				CorrelationIdHelper = new CorrelationIdHelper((ThreadedContextItemCollectionFactory)ContextFactory);
				ConfigurationManager = new ConfigurationManager();
				Logger = new TraceLogger(new LoggerSettings(), CorrelationIdHelper);
				EventStore = CreateEventStore();

				Bus = new InProcessBus<TAuthenticationToken>
				(
					(IAuthenticationTokenHelper<TAuthenticationToken>)new DefaultAuthenticationTokenHelper(ContextFactory),
					CorrelationIdHelper,
					this,
					Logger,
					ConfigurationManager,
					new BusHelper(ConfigurationManager, ContextFactory)
				);

				AggregateRepository = new AggregateRepository<TAuthenticationToken>
				(
					new AggregateFactory(this, Logger),
					EventStore,
					Bus,
					CorrelationIdHelper,
					ConfigurationManager
				);
			}

			#region Implementation of IDependencyResolver

			/// <summary>
			/// Resolves a single instance for the specified <typeparamref name="T"/>.
			///             Different implementations may return the first or last instance found or may return an exception.
			/// </summary>
			/// <typeparam name="T">The <see cref="T:System.Type"/> of object you want to resolve.</typeparam>
			/// <returns>
			/// An instance of type <typeparamref name="T"/>.
			/// </returns>
			public override T Resolve<T>()
			{
				return (T)Resolve(typeof(T));
			}

			/// <summary>
			/// Resolves a single instance for the specified <paramref name="type"/>.
			///             Different implementations may return the first or last instance found or may return an exception.
			/// </summary>
			/// <param name="type">The <see cref="T:System.Type"/> of object you want to resolve.</param>
			/// <returns>
			/// An instance of type <paramref name="type"/>.
			/// </returns>
			public override object Resolve(Type type)
			{
				if (type == typeof(IContextItemCollectionFactory))
					return ContextFactory;
				if (type == typeof(ICorrelationIdHelper))
					return CorrelationIdHelper;
				if (type == typeof(IConfigurationManager))
					return ConfigurationManager;
				if (type == typeof(IEventHandlerRegistrar))
					return Bus;
				if (type == typeof(ICommandHandlerRegistrar))
					return Bus;
				if (type == typeof(IEventPublisher<TAuthenticationToken>))
					return Bus;
				if (type == typeof(ICommandPublisher<TAuthenticationToken>))
					return Bus;
				if (type == typeof(IEventReceiver<TAuthenticationToken>))
					return Bus;
				if (type == typeof(ICommandReceiver<TAuthenticationToken>))
					return Bus;
				if (type == typeof(IEventReceiver))
					return Bus;
				if (type == typeof(ICommandReceiver))
					return Bus;
				if (type == typeof(ILogger))
					return Logger;
				if (type == typeof(IUnitOfWork<TAuthenticationToken>))
					return new UnitOfWork<TAuthenticationToken>(AggregateRepository);
				if (type == typeof(IAggregateRepository<TAuthenticationToken>))
					return AggregateRepository;
				if (type == typeof(IEventStore<TAuthenticationToken>))
					return EventStore;

				if (typeof(ICommandHandle).IsAssignableFrom(type))
					return Activator.CreateInstance(type, Resolve<IUnitOfWork<TAuthenticationToken>>());
				if (typeof(IEventHandler).IsAssignableFrom(type))
					return Activator.CreateInstance(type);

				return Activator.CreateInstance(type);
			}

			#endregion

			private IEventStore<TAuthenticationToken> CreateEventStore()
			{
				return new InProcessEventStore<TAuthenticationToken>();
			}

			#region Implementation of IDisposable

			/// <summary>
			/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
			/// </summary>
			public void Dispose()
			{
				ContextFactory = null;
				CorrelationIdHelper = null;
				ConfigurationManager = null;
				Logger = null;
				EventStore = null;
				Bus = null;
				AggregateRepository = null;
			}

			#endregion
		}
	}
}