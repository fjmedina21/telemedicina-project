export type Identity = {
  userId: string;
  email: string;
  role: string;
  permissions: string[];
  accountVersion: number;
};
export function homeFor(role: string): string {
  return role === "Admission" ? "/admision" : role === "Administrator" ? "/personal" : "/cuenta";
}
