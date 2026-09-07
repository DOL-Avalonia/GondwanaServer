using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;

namespace DOL.GS
{
    public sealed class ZoneService : GameServiceBase
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);
        private ServiceObjectView<SubZoneObject> _view;

        public static ZoneService Instance { get; } = new();

        public override void Tick()
        {
            ProcessPostedActionsParallel();

            try
            {
                _view = ServiceObjectStore.UpdateAndGetView<SubZoneObject>(ServiceObjectType.SubZoneObject);
            }
            catch (Exception e)
            {
                log.Error("UpdateAndGetView failed", e);
                return;
            }

            _view.ExecuteForEach(TickInternal);
        }

        private static void TickInternal(SubZoneObject subZoneObject)
        {
            try
            {
                SubZone currentSubZone = subZoneObject.CurrentSubZone;
                SubZone destinationSubZone = subZoneObject.DestinationSubZone;

                if (currentSubZone == destinationSubZone)
                    return;

                LinkedListNode<GameObject> node = subZoneObject.Node;
                Zone currentZone = currentSubZone?.ParentZone;
                Zone destinationZone = subZoneObject.DestinationZone;
                bool changingZone = currentZone != destinationZone;

                if (currentSubZone != null)
                {
                    if (destinationSubZone != null)
                    {
                        destinationSubZone.AddObjectToThisAndRemoveFromOther(node, currentSubZone);

                        if (changingZone)
                        {
                            currentZone.OnObjectRemovedFromZone();
                            destinationZone.OnObjectAddedToZone();
                        }
                    }
                    else
                    {
                        currentSubZone.RemoveObject(node);

                        if (changingZone)
                            currentZone.OnObjectRemovedFromZone();
                    }
                }
                else
                {
                    destinationSubZone.AddObject(node);

                    if (changingZone)
                        destinationZone.OnObjectAddedToZone();
                }
            }
            catch (Exception e)
            {
                GameServiceUtils.HandleServiceException(e, nameof(ZoneService), subZoneObject, subZoneObject.Node?.Value);
            }
            finally
            {
                subZoneObject?.OnSubZoneTransition();
            }
        }
    }
}