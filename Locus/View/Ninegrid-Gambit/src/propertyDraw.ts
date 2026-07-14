import {
  publicInspectorPropertyDrawerLibrary,
  registerInspectorPropertyDrawer,
  type InspectorPropertyDrawerRegistration,
} from "@locus/view-runtime";

export const projectPropertyDrawerLibrary = publicInspectorPropertyDrawerLibrary;

export function registerProjectPropertyDrawer(
  registration: InspectorPropertyDrawerRegistration,
) {
  if (
    !registration.type &&
    !registration.valueType &&
    !registration.fieldType &&
    !registration.attribute &&
    !registration.propertyPath &&
    !registration.name &&
    !registration.drawerKind &&
    !registration.match
  ) return () => undefined;
  return projectPropertyDrawerLibrary.register(registration);
}

export { registerInspectorPropertyDrawer };
