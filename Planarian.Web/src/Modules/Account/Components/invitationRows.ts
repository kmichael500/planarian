import Papa from "papaparse";
import { InviteUserRequest } from "../Models/InviteUserRequest";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";

export const MAX_BULK_INVITATIONS = 50;

export interface InvitationRow extends InviteUserRequest {
  id: number;
}

export type InvitationDisposition = "ready" | "invalid" | "skipped";

export interface InvitationPreview {
  row: InvitationRow;
  disposition: InvitationDisposition;
  reason: string;
}

const normalizeHeader = (value: string) =>
  value
    .trim()
    .toLowerCase()
    .replace(/[\s_-]+/g, "");

const headerNames = {
  firstName: ["firstname", "first"],
  lastName: ["lastname", "last"],
  emailAddress: ["email", "emailaddress"],
};

export function parseInvitationRows(text: string): InviteUserRequest[] {
  const source = text.replace(/^\uFEFF/, "").trim();
  if (!source) throw new Error("Paste at least one person.");
  const delimiter = source.split(/\r?\n/, 1)[0].includes("\t") ? "\t" : ",";
  const parsed = Papa.parse<string[]>(source, {
    delimiter,
    skipEmptyLines: true,
  });
  if (parsed.errors.length) {
    throw new Error(
      "The data must contain three columns: first name, last name, email."
    );
  }
  const records = parsed.data;
  if (records.length === 0) throw new Error("Paste at least one person.");

  const first = records[0].map(normalizeHeader);
  const indexes = [
    first.findIndex((name) => headerNames.firstName.includes(name)),
    first.findIndex((name) => headerNames.lastName.includes(name)),
    first.findIndex((name) => headerNames.emailAddress.includes(name)),
  ];
  const isHeader = indexes.every((index) => index >= 0);
  const looksLikeHeader = indexes.some((index) => index >= 0);
  if (looksLikeHeader && !isHeader) {
    throw new Error(
      "The header must include First Name, Last Name, and Email."
    );
  }
  const data = isHeader ? records.slice(1) : records;
  if (data.length === 0) throw new Error("No invitation rows were found.");
  if (data.length > MAX_BULK_INVITATIONS) {
    throw new Error(
      `Use no more than ${MAX_BULK_INVITATIONS} people per batch.`
    );
  }

  return data.map((record, index) => {
    if (record.length !== 3) {
      throw new Error(`Row ${index + 1} must contain exactly three columns.`);
    }
    const fields = isHeader
      ? indexes.map((position) => record[position])
      : record;
    return {
      firstName: fields[0].trim(),
      lastName: fields[1].trim(),
      emailAddress: fields[2].trim(),
    };
  });
}

const validEmail = (email: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);

export function previewInvitations(
  rows: InvitationRow[],
  currentUsers: UserManagerGridVm[]
): InvitationPreview[] {
  const seen = new Set<string>();
  const existing = new Map(
    currentUsers.map(
      (user) => [user.emailAddress.trim().toLowerCase(), user] as const
    )
  );
  return rows.map((row) => {
    const firstName = row.firstName.trim();
    const lastName = row.lastName.trim();
    const email = row.emailAddress.trim().toLowerCase();

    if (!firstName || !lastName || !validEmail(email)) {
      return {
        row,
        disposition: "invalid",
        reason:
          !firstName || !lastName
            ? "First and last name are required."
            : "Enter a valid email address.",
      };
    }
    if (seen.has(email)) {
      return {
        row,
        disposition: "skipped",
        reason: "Duplicate email in this batch.",
      };
    }
    seen.add(email);

    const member = existing.get(email);
    if (member?.accessDisabledOn) {
      return {
        row,
        disposition: "skipped",
        reason: "Access disabled — use Restore access.",
      };
    }
    if (member?.hasActiveInvitation) {
      return {
        row,
        disposition: "skipped",
        reason: "Already invited — use Resend.",
      };
    }
    if (member) {
      return { row, disposition: "skipped", reason: "Already a member." };
    }
    return { row, disposition: "ready", reason: "Ready to invite" };
  });
}
