export interface User {
  id: string;
  name: string;
  picture?: string;
}
export interface Group {
  id: string;
  name: string;
  description: string;
  currency: string;
  ownerId: string;
  version: string;
  myMemberId?: string;
  netMinor?: number;
}
export interface Member {
  id: string;
  userId: string;
  name: string;
  hasHistory: boolean;
}
export interface Split {
  memberId: string;
  amountMinor: number;
}
export interface Expense {
  id: string;
  groupId: string;
  title: string;
  amountMinor: number;
  date: string;
  payerId: string;
  splitMode: 'Equal' | 'Custom';
  note: string;
  createdBy: string;
  version: string;
  splits: Split[];
}
export interface Balance {
  memberId: string;
  name: string;
  paidMinor: number;
  shareMinor: number;
  sentMinor: number;
  receivedMinor: number;
  netMinor: number;
}
export interface Suggestion {
  fromId: string;
  toId: string;
  amountMinor: number;
}
export interface Snapshot {
  totalMinor: number;
  members: Balance[];
  suggestions: Suggestion[];
}
export interface Settlement {
  id: string;
  fromId: string;
  toId: string;
  amountMinor: number;
  date: string;
  note: string;
  createdBy: string;
  voidedAt: string | null;
}
export interface Invitation {
  id: string;
  expiresAt: string;
  revokedAt: string | null;
}
export interface Page<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
