using System.Net;
using Hl7.Fhir.Model;
using Ihis.FhirEngine.Core;
using Ihis.FhirEngine.Core.Data;
using Ihis.FhirEngine.Core.Exceptions;
using Task = System.Threading.Tasks.Task;

namespace Synapxe.FhirWebApi1.Handlers
{
    [FhirHandlerClass(AcceptedType = nameof(CarePlan))]
    public class PutHealthPlanHandler
    {
        private readonly IDataService dataStore;

        public PutHealthPlanHandler(IDataService<CarePlan> dataStore)
        {
            this.dataStore = dataStore;
        }

        [FhirHandler("PreInteraction_create_health_plan_data_preparation", HandlerCategory.PreInteraction,
            FhirInteractionType.OperationType, sort: 99, CustomOperation = "put-health-plan")]
        public Task PreInteractionPrepareDataForCreatingHealPlanAsync(IFhirContext context, CancellationToken cancellationToken)
        {
            var healthPlan = (context.Request.Resource as CarePlan)!;
            healthPlan.Meta = new Meta { Profile = new[] { "http://ihis.sg/StructureDefinition/CarePlan-pophealth-plan" } };

            AssignCarePlanSubjectToTargetSubjects(healthPlan);

            SetGoalAndConditionProfileMetaData(healthPlan);

            return Task.CompletedTask;
        }

        [FhirHandler("CRUD_create_health_plan", HandlerCategory.CRUD, FhirInteractionType.OperationType,
            CustomOperation = "put-health-plan")]
        public async Task CreateAsync(
            IFhirContext context,
            CarePlan healthPlan,
            CancellationToken cancellationToken = default)
        {
            await dataStore.UpsertAsync(healthPlan, null, true, true, false, cancellationToken);

            context.Response.StatusCode = HttpStatusCode.OK;
            context.Response.AddResource(new OperationOutcome { Text = new Narrative("OK") });
        }

        private static void AssignCarePlanSubjectToTargetSubjects(CarePlan healthPlan)
        {
            ResourceReference subject = healthPlan.Subject;
            foreach (Resource resource in healthPlan.Contained)
            {
                switch (resource)
                {
                    case Goal goal:
                        goal.Subject = subject;
                        break;
                    case Condition condition:
                        condition.Subject = subject;
                        break;
                }
            }
        }

        private static void SetGoalAndConditionProfileMetaData(CarePlan healthPlan)
        {
            foreach (Resource resource in healthPlan.Contained)
            {
                switch (resource)
                {
                    case Goal goal:
                        goal.Meta = IsValidGoal(goal)
                            ? new Meta
                                { Profile = new[] { $"http://ihis.sg/profile/{goal.Description.Coding[0].Code}" } }
                            : throw new ResourceNotFoundException("Goal structure issue");
                        break;
                    case Condition condition:
                        condition.Meta = IsValidCondition(condition)
                            ? new Meta
                                { Profile = new[] { $"http://ihis.sg/profile/C1" } }
                            : throw new ResourceNotFoundException("Condition structure issue");
                        break;
                }
            }
        }

        private static bool IsValidGoal(Goal goal)
        {
            string[] goals = { "G1", "G2", "G3", "G4", "G5", "G6", "G7", "G8" };
            return goal.Description.Coding.Count == 1 && goals.Contains(goal.Description.Coding[0].Code);
        }

        private static bool IsValidCondition(Condition condition)
        {
            return condition.Category.Count == 1 &&
                   condition.Category[0].Coding.Count == 1 &&
                   condition.Category[0].Coding[0].Code == "patient-condition-tag";
        }
    }
}
