using DetectChangesPayloadJoinEntity;
using Optional;
using Required;
using SaveChangesPayloadJoinEntity;
using WithJoinEntity;
using WithJoinEntityAndSkips;
using WithPayloadJoinEntity;
using WithTwoPayloadJoinEntity;

public class Program
{
    public static void Main()
    {
        RequiredRelationships.DeleteOrphan();
        RequiredRelationships.ReparentPost();
        RequiredRelationships.ThrowForOrphan();
        RequiredRelationships.AddNewAsset();
        RequiredRelationships.DeleteBlog();
        
        OptionalRelationships.FixupByQuery();
        OptionalRelationships.FixupByMultiQuery();
        OptionalRelationships.ChangeCollection();
        OptionalRelationships.ChangeReference();
        OptionalRelationships.ChangeFk();
        OptionalRelationships.ChangeCollectionMinimal();
        OptionalRelationships.RemovePost();
        OptionalRelationships.AddNewAsset();
        OptionalRelationships.DeleteBlog();

        ExplicitJoinEntity.AssociateByFk();
        ExplicitJoinEntity.AssociateByReference();
        ExplicitJoinEntityWithSkips.AssociateBySkip();
        OptionalRelationships.AssociateBySkip();
        ExplicitJoinWithPayload.AssociateBySkip();
        ExplicitJoinWithTwoPayloads.AssociateBySkip();
        ExplicitJoinWithSaveChanges.AssociateBySkip();
        ExplicitJoinWithDetectChangesAvoidance.AssociateBySkip();
    }
}
