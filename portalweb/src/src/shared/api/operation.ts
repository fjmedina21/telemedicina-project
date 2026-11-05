export type OperationRunner=(action:()=>Promise<void>)=>Promise<boolean>;
