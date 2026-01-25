## Introduction
This is the default code generator project for [Onyx](https://github.com/Ak-Elements/Onyx).

## Features
###Component & Field Attributes
| Attribute      | Description |
|----------------|-------------|
| ReadOnly       | Value is visible but cannot be edited in the property grid |
| Hidden         | Never shown in the property grid |
| RuntimeOnly    | Only shown in the property grid when *Show All* is enabled |
| Transient      | Component / Field is not serialized |
| EditorOnly     | Only available in editor builds |
| Description   | Long-form description text |
| Name          | Display name override |
| Tooltip       | Short tooltip shown in the editor |

| Attribute     | Serialized | Editor Build | Runtime Build | Property Grid Visibility |
|---------------|------------|--------------|---------------|--------------------------|
| Hidden        | -        | -           | -            | ❌              |
| Transient     | ❌         | -         | -           |    -           |
| EditorOnly    | -        | ✅          | ❌            |   -              |

### Field Attributes

#### Numeric Types Only
| Attribute | Description |
|----------|-------------|
| Min      | Minimum allowed value |
| Max      | Maximum allowed value |
| Range    | Defines both minimum and maximum value |

## License

Required Notice: Copyright AkElements

This repository is licensed under the PolyForm Noncommercial License 1.0.0
https://polyformproject.org/licenses/noncommercial/1.0.0/

For questions regarding the license or if you would like to have a more permissive license feel free to contact me. (akelements.dev@gmail.com)
