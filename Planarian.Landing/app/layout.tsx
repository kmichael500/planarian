import type { Metadata } from "next";
import "./globals.css";
export const metadata: Metadata = { title: "Planarian — Cave Data Management", description: "Planarian helps state cave surveys and other cave-data groups manage access-controlled cave records, files, search, maps, and permissions in one place.", other: { "codex-preview": "development" } };
export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) { return <html lang="en"><body>{children}</body></html>; }
