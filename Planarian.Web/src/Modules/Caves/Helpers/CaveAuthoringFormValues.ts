import { FormInstance } from "antd";
import { AddCaveVm } from "../Models/AddCaveVm";

/**
 * Ant Form's onFinish payload only contains registered fields. Cave authoring intentionally
 * feature-gates some controls, so an existing value can remain in the form store without a
 * mounted Form.Item. Read the complete validated store before calling an authoring API so
 * "not visible / not edited" never becomes an accidental delete.
 */
export const getCaveAuthoringSubmissionValues = (
  form: FormInstance<AddCaveVm>
): AddCaveVm => form.getFieldsValue(true) as AddCaveVm;
