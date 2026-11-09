/// The backend sends enums as PascalCase ("DrivingLicence"); Dart enums are camelCase (drivingLicence).
T enumFromApi<T extends Enum>(List<T> values, String value) =>
    values.byName(value[0].toLowerCase() + value.substring(1));

String enumToApi(Enum value) => value.name[0].toUpperCase() + value.name.substring(1);
