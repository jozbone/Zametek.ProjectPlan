﻿using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Event.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ManagedActivityViewModel
        : BindableBase, IManagedActivityViewModel
    {
        #region Fields

        private DateTime? m_MinimumEarliestStartDateTime;
        private DateTime m_ProjectStart;
        private bool m_HasUpdatedDependencies;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IEventAggregator m_EventService;

        #endregion

        #region Ctors

        private ManagedActivityViewModel(
            IDateTimeCalculator dateTimeCalculator,
            IEventAggregator eventService)
        {
            m_DateTimeCalculator = dateTimeCalculator ?? throw new ArgumentNullException(nameof(dateTimeCalculator));
            m_EventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
            ResourceSelector = new ResourceSelectorViewModel();
            UpdateAllocatedToResources();
            UpdatedDependencies = new HashSet<int>();
        }

        public ManagedActivityViewModel(
            IDependentActivity<int, int> dependentActivity,
            DateTime projectStart,
            IEnumerable<ResourceModel> targetResources,
            IDateTimeCalculator dateTimeCalculator,
            IEventAggregator eventService)
            : this(dateTimeCalculator, eventService)
        {
            DependentActivity = dependentActivity ?? throw new ArgumentNullException(nameof(dependentActivity));
            m_ProjectStart = projectStart;
            var selectedResources = new HashSet<int>(DependentActivity.TargetResources.ToList());
            ResourceSelector.SetTargetResources(targetResources, selectedResources);
            UpdateAllocatedToResources();

            if (MinimumEarliestStartDateTime.HasValue)
            {
                CalculateMinimumEarliestStartTime();
            }
            else if (MinimumEarliestStartTime.HasValue)
            {
                CalculateMinimumEarliestStartDateTime();
            }
        }

        #endregion

        #region Properties

        public IDependentActivity<int, int> DependentActivity { get; }

        public ResourceSelectorViewModel ResourceSelector
        {
            get;
            private set;
        }

        public DateTime? EarliestStartDateTime
        {
            get
            {
                if (EarliestStartTime.HasValue)
                {
                    return m_DateTimeCalculator.AddDays(ProjectStart, EarliestStartTime.GetValueOrDefault());
                }
                return null;
            }
        }

        public DateTime? LatestStartDateTime
        {
            get
            {
                if (LatestStartTime.HasValue)
                {
                    return m_DateTimeCalculator.AddDays(ProjectStart, LatestStartTime.GetValueOrDefault());
                }
                return null;
            }
        }

        public DateTime? EarliestFinishDateTime
        {
            get
            {
                if (EarliestFinishTime.HasValue)
                {
                    return m_DateTimeCalculator.AddDays(ProjectStart, EarliestFinishTime.GetValueOrDefault());
                }
                return null;
            }
        }

        public DateTime? LatestFinishDateTime
        {
            get
            {
                if (LatestFinishTime.HasValue)
                {
                    return m_DateTimeCalculator.AddDays(ProjectStart, LatestFinishTime.GetValueOrDefault());
                }
                return null;
            }
        }

        #endregion

        #region Private Methods

        private void PublishManagedActivityUpdatedPayload()
        {
            m_EventService.GetEvent<PubSubEvent<ManagedActivityUpdatedPayload>>()
                .Publish(new ManagedActivityUpdatedPayload());
        }

        private void CalculateMinimumEarliestStartDateTime()
        {
            int? minimumEarliestStartTime = DependentActivity.MinimumEarliestStartTime;
            if (minimumEarliestStartTime.HasValue)
            {
                m_MinimumEarliestStartDateTime = m_DateTimeCalculator.AddDays(ProjectStart, minimumEarliestStartTime.GetValueOrDefault());
            }
            else
            {
                m_MinimumEarliestStartDateTime = null;
            }
        }

        private void CalculateMinimumEarliestStartTime()
        {
            DateTime? minimumEarliestStartDateTime = m_MinimumEarliestStartDateTime;
            if (minimumEarliestStartDateTime.HasValue)
            {
                DependentActivity.MinimumEarliestStartTime = m_DateTimeCalculator.CountDays(ProjectStart, minimumEarliestStartDateTime.GetValueOrDefault());
            }
            else
            {
                DependentActivity.MinimumEarliestStartTime = null;
            }
        }

        private void UpdateTargetResources()
        {
            DependentActivity.TargetResources.Clear();
            DependentActivity.TargetResources.UnionWith(ResourceSelector.SelectedResourceIds);
            RaisePropertyChanged(nameof(TargetResources));
            RaisePropertyChanged(nameof(ResourceSelector));
        }

        #endregion

        #region IManagedActivityViewModel Members

        public DateTime ProjectStart
        {
            get
            {
                return m_ProjectStart;
            }
            set
            {
                m_ProjectStart = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(EarliestStartDateTime));
                RaisePropertyChanged(nameof(LatestStartDateTime));
                RaisePropertyChanged(nameof(EarliestFinishDateTime));
                RaisePropertyChanged(nameof(LatestFinishDateTime));
                CalculateMinimumEarliestStartTime();
                RaisePropertyChanged(nameof(MinimumEarliestStartTime));
            }
        }

        public bool UseBusinessDays
        {
            set
            {
                m_DateTimeCalculator.UseBusinessDays(value);
                RaisePropertyChanged(nameof(EarliestStartDateTime));
                RaisePropertyChanged(nameof(LatestStartDateTime));
                RaisePropertyChanged(nameof(EarliestFinishDateTime));
                RaisePropertyChanged(nameof(LatestFinishDateTime));
                CalculateMinimumEarliestStartTime();
                RaisePropertyChanged(nameof(MinimumEarliestStartTime));
            }
        }

        public string DependenciesString
        {
            get
            {
                return string.Join(DependenciesStringValidationRule.Separator.ToString(), Dependencies.OrderBy(x => x));
            }
            set
            {
                string stripped = DependenciesStringValidationRule.StripWhitespace(value);
                IList<int> updatedDependencies = DependenciesStringValidationRule.Parse(stripped);
                UpdatedDependencies.Clear();
                UpdatedDependencies.UnionWith(updatedDependencies);
                HasUpdatedDependencies = true;
                RaisePropertyChanged();
            }
        }

        public HashSet<int> UpdatedDependencies
        {
            get;
        }

        public bool HasUpdatedDependencies
        {
            get
            {
                return m_HasUpdatedDependencies;
            }
            set
            {
                m_HasUpdatedDependencies = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(Dependencies));
                RaisePropertyChanged(nameof(DependenciesString));
            }
        }

        public string ResourceDependenciesString => string.Join(DependenciesStringValidationRule.Separator.ToString(), ResourceDependencies.OrderBy(x => x));

        public int Id => DependentActivity.Id;

        public bool CanBeRemoved => DependentActivity.CanBeRemoved;

        public string Name
        {
            get
            {
                return DependentActivity.Name;
            }
            set
            {
                DependentActivity.Name = value;
                RaisePropertyChanged();
            }
        }

        public HashSet<int> TargetResources => DependentActivity.TargetResources;

        public LogicalOperator TargetResourceOperator
        {
            get
            {
                return DependentActivity.TargetResourceOperator;
            }
            set
            {
                DependentActivity.TargetResourceOperator = value;
                RaisePropertyChanged();
            }
        }

        public HashSet<int> AllocatedToResources => DependentActivity.AllocatedToResources;

        public string AllocatedToResourcesString
        {
            get
            {
                return string.Join(DependenciesStringValidationRule.Separator.ToString(), ResourceSelector
                    .TargetResources.Where(x => AllocatedToResources.Contains(x.Id))
                    .OrderBy(x => x.Id)
                    .Select(x => x.DisplayName));
            }
        }


        public bool IsDummy => DependentActivity.IsDummy;

        public int Duration
        {
            get
            {
                return DependentActivity.Duration;
            }
            set
            {
                if (value < 0)
                {
                    value = 0;
                }
                DependentActivity.Duration = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(IsDummy));
                RaisePropertyChanged(nameof(IsCritical));
                RaisePropertyChanged(nameof(EarliestFinishTime));
                RaisePropertyChanged(nameof(EarliestFinishDateTime));
                RaisePropertyChanged(nameof(LatestStartTime));
                RaisePropertyChanged(nameof(LatestStartDateTime));
                RaisePropertyChanged(nameof(TotalSlack));
                RaisePropertyChanged(nameof(InterferingSlack));
            }
        }

        public int? TotalSlack => DependentActivity.TotalSlack;

        public int? FreeSlack
        {
            get
            {
                return DependentActivity.FreeSlack;
            }
            set
            {
                DependentActivity.FreeSlack = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(InterferingSlack));
                RaisePropertyChanged(nameof(DependenciesString));
                RaisePropertyChanged(nameof(ResourceDependenciesString));
            }
        }

        public int? InterferingSlack => DependentActivity.InterferingSlack;

        public bool IsCritical => DependentActivity.IsCritical;

        public int? EarliestStartTime
        {
            get
            {
                return DependentActivity.EarliestStartTime;
            }
            set
            {
                DependentActivity.EarliestStartTime = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(EarliestStartDateTime));
                RaisePropertyChanged(nameof(EarliestFinishTime));
                RaisePropertyChanged(nameof(EarliestFinishDateTime));
                RaisePropertyChanged(nameof(TotalSlack));
                RaisePropertyChanged(nameof(IsCritical));
                RaisePropertyChanged(nameof(InterferingSlack));
                RaisePropertyChanged(nameof(DependenciesString));
                RaisePropertyChanged(nameof(ResourceDependenciesString));
            }
        }

        public int? LatestStartTime => DependentActivity.LatestStartTime;

        public int? EarliestFinishTime => DependentActivity.EarliestFinishTime;

        public int? LatestFinishTime
        {
            get
            {
                return DependentActivity.LatestFinishTime;
            }
            set
            {
                DependentActivity.LatestFinishTime = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(LatestFinishDateTime));
                RaisePropertyChanged(nameof(LatestStartTime));
                RaisePropertyChanged(nameof(LatestStartDateTime));
                RaisePropertyChanged(nameof(TotalSlack));
                RaisePropertyChanged(nameof(IsCritical));
                RaisePropertyChanged(nameof(InterferingSlack));
                RaisePropertyChanged(nameof(DependenciesString));
                RaisePropertyChanged(nameof(ResourceDependenciesString));
            }
        }

        public int? MinimumFreeSlack
        {
            get
            {
                return DependentActivity.MinimumFreeSlack;
            }
            set
            {
                if (value < 0)
                {
                    value = 0;
                }
                DependentActivity.MinimumFreeSlack = value;
                RaisePropertyChanged();
            }
        }

        public int? MinimumEarliestStartTime
        {
            get
            {
                return DependentActivity.MinimumEarliestStartTime;
            }
            set
            {
                if (value < 0)
                {
                    value = 0;
                }
                DependentActivity.MinimumEarliestStartTime = value;
                RaisePropertyChanged();
                CalculateMinimumEarliestStartDateTime();
                RaisePropertyChanged(nameof(MinimumEarliestStartDateTime));
            }
        }

        public DateTime? MinimumEarliestStartDateTime
        {
            get
            {
                return m_MinimumEarliestStartDateTime;
            }
            set
            {
                if (value.HasValue)
                {
                    if (value < ProjectStart)
                    {
                        value = ProjectStart;
                    }
                    value = value.GetValueOrDefault().Date + ProjectStart.TimeOfDay;
                }
                m_MinimumEarliestStartDateTime = value;
                RaisePropertyChanged();
                CalculateMinimumEarliestStartTime();
                RaisePropertyChanged(nameof(MinimumEarliestStartTime));
            }
        }

        public HashSet<int> Dependencies => DependentActivity.Dependencies;

        public HashSet<int> ResourceDependencies => DependentActivity.ResourceDependencies;

        public void SetTargetResources(IEnumerable<ResourceModel> targetResources)
        {
            if (targetResources == null)
            {
                throw new ArgumentNullException(nameof(targetResources));
            }
            UpdateTargetResources();
            var selectedTargetResources = new HashSet<int>(DependentActivity.TargetResources.ToList());
            ResourceSelector.SetTargetResources(targetResources, selectedTargetResources);
            UpdateTargetResources();
        }

        public void UpdateAllocatedToResources()
        {
            RaisePropertyChanged(nameof(AllocatedToResourcesString));
        }

        public void SetAsReadOnly()
        {
            DependentActivity.SetAsReadOnly();
        }

        public void SetAsRemovable()
        {
            DependentActivity.SetAsRemovable();
        }

        public object CloneObject()
        {
            return DependentActivity.CloneObject();
        }

        private bool _isDirty = false;

        public void BeginEdit()
        {
            // Bug Fix: Windows Controls call EndEdit twice; Once
            // from IEditableCollectionView, and once from BindingGroup.
            // This makes sure it only happens once after a BeginEdit.
            _isDirty = true;
        }

        public void EndEdit()
        {
            if (_isDirty)
            {
                _isDirty = false;
                UpdateTargetResources();
                PublishManagedActivityUpdatedPayload();
            }
        }

        public void CancelEdit()
        {
            _isDirty = false;
        }

        #endregion
    }
}
