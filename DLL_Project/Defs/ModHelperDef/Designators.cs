﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

using Verse;

namespace CommunityCoreLibrary
{

    public class MHD_Designators : IInjector
    {

        // A14 - resolvedDesignators is now private - the public accessor filters by currently allowed.
        FieldInfo _resolvedDesignatorsField = typeof( DesignationCategoryDef ).GetField( "resolvedDesignators", BindingFlags.NonPublic | BindingFlags.Instance );

        private List<Designator> _resolvedDesignators ( DesignationCategoryDef category )
        {
            return _resolvedDesignatorsField.GetValue( category ) as List<Designator>;
        }

        private bool DesignatorExists( DesignatorData designatorData )
        {
            var designationCategory = DefDatabase<DesignationCategoryDef>.GetNamed( designatorData.designationCategoryDef, false );
            List<Designator> designators = _resolvedDesignatorsField.GetValue( designationCategory ) as List<Designator>;
            return designators.Exists( d => d.GetType() == designatorData.designatorClass );
        }

#if DEBUG
        public string                       InjectString
        {
            get
            {
                return "Designators injected";
            }
        }

        public bool                         IsValid( ModHelperDef def, ref string errors )
        {
            if( def.Designators.NullOrEmpty() )
            {
                return true;
            }

            bool isValid = true;

            foreach( var designatorData in def.Designators )
            {
                var designatorType = designatorData.designatorClass;
                if(
                    ( designatorType == null )||
                    ( !designatorType.IsSubclassOf( typeof( Designator ) ) )
                )
                {
                    errors += string.Format( "Unable to resolve designatorClass '{0}'", designatorData.designatorClass );
                    isValid = false;
                }
                if(
                    ( string.IsNullOrEmpty( designatorData.designationCategoryDef ) )||
                    ( DefDatabase<DesignationCategoryDef>.GetNamed( designatorData.designationCategoryDef, false ) == null )
                )
                {
                    errors += string.Format( "Unable to resolve designationCategoryDef '{0}'", designatorData.designationCategoryDef );
                    isValid = false;
                }
                if(
                    ( designatorData.designatorNextTo != null )&&
                    ( !designatorData.designatorNextTo.IsSubclassOf( typeof( Designator ) ) )
                )
                {
                    errors += string.Format( "Unable to resolve designatorNextTo '{0}'", designatorData.designatorNextTo );
                    isValid = false;
                }
            }

            return isValid;
        }
#endif

        public bool                         Injected( ModHelperDef def )
        {
            if( def.Designators.NullOrEmpty() )
            {
                return true;
            }

            foreach( var designatorData in def.Designators )
            {
                if (!DesignatorExists( designatorData ) )
                {
                    return false;
                }
            }

            return true;
        }

        public bool                         Inject( ModHelperDef def )
        {
            if( def.Designators.NullOrEmpty() )
            {
                return true;
            }

            foreach( var designatorData in def.Designators )
            {
                // Get the category
                var designationCategory = DefDatabase<DesignationCategoryDef>.GetNamed( designatorData.designationCategoryDef, false );

                // First instatiate and inject the designator into the list of resolved designators
                if ( !DesignatorExists( designatorData ) )
                {
                    // Create the new designator
                    var designatorObject = (Designator) Activator.CreateInstance( designatorData.designatorClass );
                    if( designatorObject == null )
                    {
                        CCL_Log.Message( string.Format( "Unable to create instance of '{0}'", designatorData.designatorClass ) );
                        return false;
                    }
                          
                    if( designatorData.designatorNextTo == null )
                    {
                        // Inject the designator
                        _resolvedDesignators( designationCategory ).Add( designatorObject );
                    }
                    else
                    {
                        // Prefers to be beside a specific designator
                        var designatorIndex = _resolvedDesignators( designationCategory ).FindIndex( d => (
                            ( d.GetType() == designatorData.designatorNextTo )
                        ) );

                        if( designatorIndex < 0 )
                        {
                            // Other designator doesn't exist (yet?)
                            // Inject the designator at the end
                            _resolvedDesignators( designationCategory ).Add( designatorObject );
                        }
                        else
                        {
                            // Inject beside desired designator
                            _resolvedDesignators( designationCategory ).Insert( designatorIndex + 1, designatorObject );
                        }
                    }
                }

                // Now inject the designator class into the list of classes as a saftey net for another mod resolving the category
                if( !designationCategory.specialDesignatorClasses.Exists( s => s == designatorData.designatorClass ) )
                {
                    if( designatorData.designatorNextTo == null )
                    {
                        // Inject the designator class at the end of the list
                        designationCategory.specialDesignatorClasses.Add( designatorData.designatorClass );
                    }
                    else
                    {
                        // Prefers to be beside a specific designator
                        var designatorIndex = designationCategory.specialDesignatorClasses.FindIndex( s => s == designatorData.designatorNextTo );

                        if( designatorIndex < 0 )
                        {
                            // Can't find desired designator class
                            // Inject the designator at the end
                            designationCategory.specialDesignatorClasses.Add( designatorData.designatorClass );
                        }
                        else
                        {
                            // Inject beside desired designator class
                            designationCategory.specialDesignatorClasses.Insert( designatorIndex + 1, designatorData.designatorClass );
                        }
                    }
                }
            }

            return true;
        }

    }

}
