export default function InputBox({ label, error, ...props }) {
  return <label className="auth-field">{label}<input {...props} />{error && <small>{error}</small>}</label>;
}