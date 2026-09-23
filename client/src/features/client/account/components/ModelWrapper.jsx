export default function ModelWrapper({ children, onClose }) {
  return <div className="auth-modal-backdrop" role="presentation" onMouseDown={onClose}><div onMouseDown={(event) => event.stopPropagation()}>{children}</div></div>;
}