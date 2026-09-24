export const contractStates = {
  offered: { label: "Offered", clientAction: "Accept contract", freelancerAction: null },
  active: { label: "Active", clientAction: "Request revision", freelancerAction: "Submit deliverable" },
  submitted: { label: "Submitted", clientAction: "Approve work", freelancerAction: null },
  completed: { label: "Completed", clientAction: null, freelancerAction: null },
  disputed: { label: "Disputed", clientAction: "Raise dispute", freelancerAction: "Raise dispute" },
  cancelled: { label: "Cancelled", clientAction: null, freelancerAction: null },
};

export function getContractState(status) {
  return contractStates[String(status || "").toLowerCase()] || {
    label: status || "Unknown",
    clientAction: null,
    freelancerAction: null,
  };
}
