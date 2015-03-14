﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LCL.Repositories
{
    /// <summary>
    /// Represents the base class for domain repositories.
    /// </summary>
    public abstract class DomainRepository : DisposableObject, IDomainRepository
    {
        #region Private Fields
        private volatile bool committed;
        private readonly HashSet<ISourcedAggregateRoot> saveHash = new HashSet<ISourcedAggregateRoot>();
        private readonly Action<ISourcedAggregateRoot> delegatedUpdateAndClearAggregateRoot = ar =>
            {
                ar.GetType().GetMethod(SourcedAggregateRoot.UpdateVersionAndClearUncommittedEventsMethodName,
                    BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ar, null);
            };
        #endregion

        #region Ctor
        /// <summary>
        /// Initializes a new instance of <c>DomainRepository</c> class.
        /// </summary>
        public DomainRepository()
        {
            this.committed = false;
        }
        #endregion

        #region Protected Properties
        /// <summary>
        /// Gets the list which contains the aggregate roots being saved.
        /// </summary>
        protected HashSet<ISourcedAggregateRoot> SaveHash
        {
            get { return this.saveHash; }
        }
        /// <summary>
        /// Gets the delegated method which updates the version on aggregate root
        /// and clears all its uncommitted events.
        /// </summary>
        protected Action<ISourcedAggregateRoot> DelegatedUpdateAndClearAggregateRoot
        {
            get { return this.delegatedUpdateAndClearAggregateRoot; }
        }
        #endregion

        #region Protected Methods
        /// <summary>
        /// Commits the changes registered in the domain repository.
        /// </summary>
        protected abstract void DoCommit();
        /// <summary>
        /// Disposes the object.
        /// </summary>
        /// <param name="disposing">A <see cref="System.Boolean"/> value which indicates whether
        /// the object should be disposed explicitly.</param>
        protected override void Dispose(bool disposing) { }
        /// <summary>
        /// Creates a new instance of the aggregate root regardless whether a public
        /// default constructor is provided.
        /// </summary>
        /// <typeparam name="TAggregateRoot">The type of the aggregate root.</typeparam>
        /// <returns>A newly created instance for the specified aggregate root type.</returns>
        protected TAggregateRoot CreateAggregateRootInstance<TAggregateRoot>()
            where TAggregateRoot : class, ISourcedAggregateRoot
        {
            Type aggregateRootType = typeof(TAggregateRoot);
            ConstructorInfo constructor = aggregateRootType
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p =>
                {
                    var parameters = p.GetParameters();
                    return parameters == null || parameters.Length == 0;
                }).FirstOrDefault();
            if (constructor != null)
                return constructor.Invoke(null) as TAggregateRoot;
            throw new Exception(string.Format("At least one parameterless constructor should be defined on the aggregate root type '{0}'.", typeof(TAggregateRoot)));
        }
        #endregion

        #region IDomainRepository Members
        /// <summary>
        /// Gets the instance of the aggregate root with the specified identifier.
        /// </summary>
        /// <param name="id">The identifier of the aggregate root.</param>
        /// <returns>The instance of the aggregate root with the specified identifier.</returns>
        public abstract TAggregateRoot Get<TAggregateRoot>(Guid id)
            where TAggregateRoot : class, ISourcedAggregateRoot;
        /// <summary>
        /// Saves the aggregate represented by the specified aggregate root to the repository.
        /// </summary>
        /// <param name="aggregateRoot">The aggregate root that is going to be saved.</param>
        public virtual void Save<TAggregateRoot>(TAggregateRoot aggregateRoot)
            where TAggregateRoot : class, ISourcedAggregateRoot
        {
            if (!saveHash.Contains(aggregateRoot))
                saveHash.Add(aggregateRoot);
            committed = false;
        }
        #endregion

        #region IUnitOfWork Members
        /// <summary>
        /// Gets a <see cref="System.Boolean"/> value which indicates
        /// whether the Unit of Work could support Microsoft Distributed
        /// Transaction Coordinator (MS-DTC).
        /// </summary>
        public abstract bool DistributedTransactionSupported { get; }
        /// <summary>
        /// Gets a <see cref="System.Boolean"/> value which indicates
        /// whether the Unit of Work was successfully committed.
        /// </summary>
        public bool Committed
        {
            get { return this.committed; }
            protected set { this.committed = value; }
        }
        /// <summary>
        /// Commits the transaction.
        /// </summary>
        public void Commit()
        {
            this.DoCommit();
            this.saveHash.ToList().ForEach(this.delegatedUpdateAndClearAggregateRoot);
            this.saveHash.Clear();
            this.committed = true;
        }
        /// <summary>
        /// Rollback the transaction.
        /// </summary>
        public abstract void Rollback();
        #endregion
    }
}
