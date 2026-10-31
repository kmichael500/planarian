import { FormInstance } from "antd";
import { AddCaveVm } from "../Models/AddCaveVm";

/** Preserve feature-gated values that remain in Ant Form's store but are not currently mounted. */
export const getCaveAuthoringSubmissionValues = (
  form: FormInstance<AddCaveVm>
): AddCaveVm => form.getFieldsValue(true) as AddCaveVm;
